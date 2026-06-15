using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Application.Cashier.TransferTable;
using Rpom.Application.Configuration;
using Rpom.Domain.Access;
using Rpom.Domain.Menu;
using Rpom.Domain.Operations;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;
using Rpom.Domain.Sales.CashDrawer;
using Rpom.Infrastructure.Database;
using Rpom.Infrastructure.Pricing;
using Rpom.Infrastructure.Tables;
using Testcontainers.PostgreSql;

namespace Rpom.Application.Tests.Cashier;

/// <summary>E2 — Transfer Table: same-area move, cross-area SC policy, and reject paths.</summary>
public sealed class TransferTableTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;

    // areaA SC 5/8 ; areaB SC 10/8 (same counter1). areaC on counter2.
    private int _staffId, _tableA, _tableA2, _tableB, _tableC, _itemId;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_db.GetConnectionString(),
                o => o.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default).UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        _ctx = new ApplicationDbContext(options);
        await _ctx.Database.MigrateAsync();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task SameArea_ChangesTable_KeepsAreaScAndCart()
    {
        long ticketId = await OpenOnTableA();
        await Add().Handle(new AddCartItem.Command(ticketId, _itemId, 1m, null, []), CancellationToken.None);

        var res = await Transfer().Handle(new TransferTable.Command(ticketId, _tableA2), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.TableId.Should().Be(_tableA2);
        res.Value.ClearedCartItemCount.Should().Be(0);

        Ticket t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
        t.TableId.Should().Be(_tableA2);
        t.ServiceChargePercent.Should().Be(5m);
        (await _ctx.CartItems.CountAsync()).Should().Be(1); // cart kept
        (await _ctx.Tables.Where(x => x.Id == _tableA2).Select(x => x.Status).FirstAsync())
            .Should().Be(TableStatus.Occupied);
    }

    [Fact]
    public async Task DifferentArea_DefaultConfig_UsesTargetSc_AndClearsCart()
    {
        long ticketId = await OpenOnTableA();
        // One SENT item (price + SC snapshot at areaA), plus one DRAFT cart line.
        await Add().Handle(new AddCartItem.Command(ticketId, _itemId, 1m, null, []), CancellationToken.None);
        await Send(ticketId);
        await Add().Handle(new AddCartItem.Command(ticketId, _itemId, 1m, null, []), CancellationToken.None);

        var res = await Transfer().Handle(new TransferTable.Command(ticketId, _tableB), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.ClearedCartItemCount.Should().Be(1);

        Ticket t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
        t.TableId.Should().Be(_tableB);
        t.ServiceChargePercent.Should().Be(10m); // re-snapshot from areaB
        (await _ctx.CartItems.CountAsync()).Should().Be(0); // draft cleared

        // SENT OrderItem keeps price but SC% recomputed to areaB.
        OrderItem oi = await _ctx.OrderItems.AsNoTracking().FirstAsync(x => x.TicketId == ticketId);
        oi.ServiceChargePercent.Should().Be(10m);
        oi.UnitPrice.Should().Be(50000m); // price NOT re-priced
    }

    [Fact]
    public async Task DifferentArea_ConfigFalse_KeepsTicketSc()
    {
        long ticketId = await OpenOnTableA();
        await Add().Handle(new AddCartItem.Command(ticketId, _itemId, 1m, null, []), CancellationToken.None);
        await Send(ticketId);

        var res = await Transfer(useTargetSc: false)
            .Handle(new TransferTable.Command(ticketId, _tableB), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        Ticket t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
        t.TableId.Should().Be(_tableB);
        t.ServiceChargePercent.Should().Be(5m); // kept from areaA
        OrderItem oi = await _ctx.OrderItems.AsNoTracking().FirstAsync(x => x.TicketId == ticketId);
        oi.ServiceChargePercent.Should().Be(5m);
    }

    [Fact]
    public async Task NotOpenTicket_Fails()
    {
        long ticketId = await OpenOnTableA();
        Ticket t = await _ctx.Tickets.FirstAsync(x => x.Id == ticketId);
        t.Status = TicketStatus.Closed;
        await _ctx.SaveChangesAsync();

        var res = await Transfer().Handle(new TransferTable.Command(ticketId, _tableA2), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Ticket.NotOpen");
    }

    [Fact]
    public async Task SameTable_Fails()
    {
        long ticketId = await OpenOnTableA();
        var res = await Transfer().Handle(new TransferTable.Command(ticketId, _tableA), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Ticket.TransferSameTable");
    }

    [Fact]
    public async Task CrossCounter_Fails()
    {
        long ticketId = await OpenOnTableA();
        var res = await Transfer().Handle(new TransferTable.Command(ticketId, _tableC), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Ticket.TransferCrossCounter");
    }

    [Fact]
    public async Task WithoutLock_Fails()
    {
        // Open with lock, then transfer from a staff who never acquired the lock.
        long ticketId = await OpenOnTableA();
        var other = Substitute.For<ICurrentStaff>();
        other.StaffAccountId.Returns(_staffId + 999);
        var handler = new TransferTable.Handler(
            _ctx, other, Clock(), Guard(), TicketRecompute(), Config(null), Version());

        var res = await handler.Handle(new TransferTable.Command(ticketId, _tableA2), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("TableLock.NotHeld");
    }

    [Fact]
    public async Task TargetNotFound_Fails()
    {
        long ticketId = await OpenOnTableA();
        var res = await Transfer().Handle(new TransferTable.Command(ticketId, 999999), CancellationToken.None);
        res.IsFailure.Should().BeTrue();
        res.Error.Code.Should().Be("Table.NotFound");
    }

    // ---------- helpers ----------

    private async Task<long> OpenOnTableA()
    {
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config(null))
            .Handle(new AcquireTableLock.Command(_tableA), CancellationToken.None);
        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableA, 2, null), CancellationToken.None);
        return open.Value.TicketId;
    }

    private async Task Send(long ticketId) =>
        await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);

    private AddCartItem.Handler Add() => new(
        _ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version());

    private TransferTable.Handler Transfer(bool? useTargetSc = null) => new(
        _ctx, Staff(), Clock(), Guard(), TicketRecompute(), Config(useTargetSc), Version());

    private TableOperationGuard Guard() => new(_ctx, Clock(), Config(null));
    private CartRecomputeService Cart() => new(_ctx, Rc(), Clock());
    private TicketRecomputeService TicketRecompute() => new(_ctx, Rc(), Clock());
    private static IVersionService Version() => Substitute.For<IVersionService>();

    private static IConfigValueService Config(bool? useTargetSc)
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        if (useTargetSc is not null)
        {
            c.GetAsync(ConfigCodes.TransferUseTargetAreaServiceCharge, Arg.Any<CancellationToken>())
                .Returns(useTargetSc.Value ? "true" : "false");
        }

        return c;
    }

    private ICurrentStaff Staff()
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(_staffId);
        return s;
    }

    private static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(_ => DateTime.UtcNow);
        return c;
    }

    private IRoundingConfig Rc()
    {
        var rc = Substitute.For<IRoundingConfig>();
        foreach (var kv in RoundingKeys.Defaults)
        {
            rc.GetDigits(kv.Key).Returns(kv.Value);
        }

        return rc;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };

        var counter1 = new Counter { Name = "C1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var counter2 = new Counter { Name = "C2", DisplayOrder = 2, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var areaA = new Area { Counter = counter1, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var areaB = new Area { Counter = counter1, Name = "B", DisplayOrder = 2, IsActive = true, ServiceChargePercent = 10m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var areaC = new Area { Counter = counter2, Name = "C", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 7m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };

        var tableA = new Table { Area = areaA, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tableA2 = new Table { Area = areaA, Code = "T02", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tableB = new Table { Area = areaB, Code = "T10", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tableC = new Table { Area = areaC, Code = "T20", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter1, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uom = new Uom { Code = "phan", Name = "Phần", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item { Code = "PHO", Name = "Phở", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var entry = new PriceEntry { PriceVariant = variant, Item = item, Price = 50000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter1, counter2, areaA, areaB, areaC,
            tableA, tableA2, tableB, tableC, shift, uom, item, priceTable, variant, entry);
        await _ctx.SaveChangesAsync();

        drawer.OpenedByStaffAccountId = staff.Id;
        drawer.ShiftId = shift.Id;
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id;
        _tableA = tableA.Id;
        _tableA2 = tableA2.Id;
        _tableB = tableB.Id;
        _tableC = tableC.Id;
        _itemId = item.Id;
    }
}
