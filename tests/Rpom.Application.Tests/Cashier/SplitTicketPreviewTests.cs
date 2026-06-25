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
using SplitT = Rpom.Application.Cashier.SplitTicket.SplitTicket;
using PreviewT = Rpom.Application.Cashier.SplitTicketPreview.SplitTicketPreview;

namespace Rpom.Application.Tests.Cashier;

/// <summary>
///     E4 Split — dry-run preview: phải KHÔNG ghi DB và trả về tổng tiền y hệt split thật.
/// </summary>
public sealed class SplitTicketPreviewTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;

    private int _staffId, _tableSrc, _tableDst, _itemId;

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
    public async Task Preview_DoesNotPersist_AndMatchesActualSplit()
    {
        long srcId = await OpenWithItems(_tableSrc, qty: 3m);
        long dstId = await OpenWithItems(_tableDst, qty: 1m);
        long srcOrderItemId = await _ctx.OrderItems.AsNoTracking()
            .Where(o => o.TicketId == srcId).Select(o => o.Id).FirstAsync();

        // Snapshot DB state BEFORE preview.
        int srcOiBefore = await _ctx.OrderItems.AsNoTracking().CountAsync(o => o.TicketId == srcId);
        int dstOiBefore = await _ctx.OrderItems.AsNoTracking().CountAsync(o => o.TicketId == dstId);
        decimal srcTotalBefore = await _ctx.Tickets.AsNoTracking().Where(t => t.Id == srcId).Select(t => t.TotalAmount).FirstAsync();
        decimal dstTotalBefore = await _ctx.Tickets.AsNoTracking().Where(t => t.Id == dstId).Select(t => t.TotalAmount).FirstAsync();

        var items = new List<PreviewT.SplitItemInput> { new(srcOrderItemId, 1m) };

        // --- PREVIEW (dry-run) ---
        var preview = await Preview().Handle(
            new PreviewT.Query(srcId, dstId, null, null, items), CancellationToken.None);
        preview.IsSuccess.Should().BeTrue();
        preview.Value.MovedItemCount.Should().Be(1);

        // DB must be UNCHANGED after preview (rolled back). Read with AsNoTracking → hits DB.
        (await _ctx.OrderItems.AsNoTracking().CountAsync(o => o.TicketId == srcId)).Should().Be(srcOiBefore);
        (await _ctx.OrderItems.AsNoTracking().CountAsync(o => o.TicketId == dstId)).Should().Be(dstOiBefore);
        (await _ctx.Tickets.AsNoTracking().Where(t => t.Id == srcId).Select(t => t.TotalAmount).FirstAsync())
            .Should().Be(srcTotalBefore);
        (await _ctx.Tickets.AsNoTracking().Where(t => t.Id == dstId).Select(t => t.TotalAmount).FirstAsync())
            .Should().Be(dstTotalBefore);
        long srcQtyAfterPreview = await _ctx.OrderItems.AsNoTracking()
            .Where(o => o.Id == srcOrderItemId).Select(o => (long)o.Quantity).FirstAsync();
        srcQtyAfterPreview.Should().Be(3); // still full qty on source

        // Reset change tracker so the real split runs on a clean context (mirrors a fresh request).
        _ctx.ChangeTracker.Clear();

        // --- ACTUAL SPLIT (commits) ---
        var real = await Split().Handle(
            new SplitT.Command(srcId, dstId, null, null,
                new List<SplitT.SplitItemInput> { new(srcOrderItemId, 1m) }), CancellationToken.None);
        real.IsSuccess.Should().BeTrue();

        // Preview totals must EQUAL the real split totals (exact, same engine).
        preview.Value.SourceTotalAmount.Should().Be(real.Value.SourceTotalAmount);
        preview.Value.DestinationTotalAmount.Should().Be(real.Value.DestinationTotalAmount);

        // And the real split actually persisted (source qty reduced, dest gained a line).
        (await _ctx.OrderItems.AsNoTracking().Where(o => o.Id == srcOrderItemId).Select(o => (long)o.Quantity).FirstAsync())
            .Should().Be(2);
        (await _ctx.OrderItems.AsNoTracking().CountAsync(o => o.TicketId == dstId)).Should().Be(dstOiBefore + 1);
    }

    [Fact]
    public async Task Preview_PropagatesFailure_WhenSourcePaid()
    {
        long srcId = await OpenWithItems(_tableSrc, qty: 1m);
        long dstId = await OpenWithItems(_tableDst, qty: 1m);
        long srcOrderItemId = await _ctx.OrderItems.AsNoTracking()
            .Where(o => o.TicketId == srcId).Select(o => o.Id).FirstAsync();

        // Mark source as paid → split (and preview) must reject.
        Ticket src = await _ctx.Tickets.FirstAsync(t => t.Id == srcId);
        src.PaidAmount = 100m;
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var preview = await Preview().Handle(
            new PreviewT.Query(srcId, dstId, null, null,
                new List<PreviewT.SplitItemInput> { new(srcOrderItemId, 1m) }), CancellationToken.None);

        preview.IsFailure.Should().BeTrue();
        preview.Error.Code.Should().Be("Ticket.SplitSourcePaid");
    }

    // ---------- helpers ----------

    private async Task<long> OpenWithItems(int tableId, decimal qty)
    {
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(tableId), CancellationToken.None);
        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(tableId, 2, null), CancellationToken.None);
        long ticketId = open.Value.TicketId;
        await Add().Handle(new AddCartItem.Command(ticketId, _itemId, qty, null, []), CancellationToken.None);
        await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);
        return ticketId;
    }

    private SplitT.Handler Split() => new(
        _ctx, Staff(), Clock(), Guard(), Config(), TicketRecompute(), RefreshPayments(), Version());

    private PreviewT.Handler Preview() => new(_ctx, Split());

    private AddCartItem.Handler Add() => new(
        _ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version());

    private TableOperationGuard Guard() => new(_ctx, Clock(), Config());
    private CartRecomputeService Cart() => new(_ctx, Rc(), Clock());
    private TicketRecomputeService TicketRecompute() => new(_ctx, Rc(), Clock());
    private RefreshPaymentTotalsService RefreshPayments() => new(_ctx, Clock());
    private static IVersionService Version() => Substitute.For<IVersionService>();

    private static IConfigValueService Config()
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
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

        var counter = new Counter { Name = "C1", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };

        var tableSrc = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var tableDst = new Table { Area = area, Code = "T02", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uom = new Uom { Code = "phan", Name = "Phần", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item { Code = "PHO", Name = "Phở", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var entry = new PriceEntry { PriceVariant = variant, Item = item, Price = 50000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter, area, tableSrc, tableDst, shift, uom, item, priceTable, variant, entry);
        await _ctx.SaveChangesAsync();

        drawer.OpenedByStaffAccountId = staff.Id;
        drawer.ShiftId = shift.Id;
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id;
        _tableSrc = tableSrc.Id;
        _tableDst = tableDst.Id;
        _itemId = item.Id;
    }
}
