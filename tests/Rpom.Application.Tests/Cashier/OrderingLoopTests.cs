using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Application.Cashier.RemoveCartItem;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Application.Cashier.UpdateCartItem;
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

/// <summary>End-to-end W1 ordering loop: open ticket → add/update/remove cart → send order.</summary>
public sealed class OrderingLoopTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _tableId, _shiftId, _singleItemId, _item2Id;

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
    public async Task FullLoop_Open_Add_Update_Send()
    {
        await AcquireLock();

        // Open ticket
        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None);
        open.IsSuccess.Should().BeTrue();
        open.Value.Code.Should().StartWith("TK-");
        var ticketId = open.Value.TicketId;

        // Table now OCCUPIED
        (await _ctx.Tables.Where(t => t.Id == _tableId).Select(t => t.Status).FirstAsync())
            .Should().Be(TableStatus.Occupied);

        // Add a single item, qty 2
        var add = await Add().Handle(
            new AddCartItem.Command(ticketId, _singleItemId, 2m, "ít cay", []), CancellationToken.None);
        add.IsSuccess.Should().BeTrue();
        add.Value.LineTotal.Should().BeGreaterThan(0);
        var cartItemId = add.Value.CartItemId;

        // Update qty → 3
        var upd = await new UpdateCartItem.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new UpdateCartItem.Command(ticketId, cartItemId, 3m, null, null), CancellationToken.None);
        upd.IsSuccess.Should().BeTrue();

        // Send order
        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);
        send.IsSuccess.Should().BeTrue();
        send.Value.ItemCount.Should().Be(1);
        send.Value.TotalAmount.Should().BeGreaterThan(0);

        // Cart cleared, OrderItem present, order SENT
        (await _ctx.CartItems.AnyAsync()).Should().BeFalse();
        (await _ctx.OrderItems.CountAsync(oi => oi.TicketId == ticketId)).Should().Be(1);
        (await _ctx.Orders.Where(o => o.TicketId == ticketId).Select(o => o.Status).FirstAsync())
            .Should().Be(OrderStatus.Sent);
        (await _ctx.OrderItems.Where(oi => oi.TicketId == ticketId).Select(oi => oi.Quantity).FirstAsync())
            .Should().Be(3m);
    }

    [Fact]
    public async Task Open_WithoutLock_Fails()
    {
        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None);
        open.IsFailure.Should().BeTrue();
        open.Error.Code.Should().Be("TableLock.NotHeld");
    }

    [Fact]
    public async Task Open_NoCashDrawer_Fails()
    {
        // Close the drawer first.
        var drawer = await _ctx.CashDrawerSessions.FirstAsync();
        drawer.Status = CashDrawerStatus.Closed;
        await _ctx.SaveChangesAsync();
        await AcquireLock();

        var open = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None);
        open.IsFailure.Should().BeTrue();
        open.Error.Code.Should().Be("Ticket.NoOpenCashDrawer");
    }

    [Fact]
    public async Task Send_EmptyCart_Fails()
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None)).Value.TicketId;

        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);
        send.IsFailure.Should().BeTrue();
        send.Error.Code.Should().Be("Order.EmptyCart");
    }

    [Fact]
    public async Task PartialSend_KeepsRemainingInNewDraftBatch()
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None)).Value.TicketId;

        // Two lines in one draft order.
        var a = await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);
        var b = await Add().Handle(new AddCartItem.Command(ticketId, _item2Id, 2m, null, []), CancellationToken.None);

        // Send only line A.
        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, new[] { a.Value.CartItemId }), CancellationToken.None);
        send.IsSuccess.Should().BeTrue();
        send.Value.ItemCount.Should().Be(1);

        // Exactly the kept line B remains in the cart, now under a NEW draft order.
        var carts = await _ctx.CartItems
            .Where(c => _ctx.Orders.Any(o => o.Id == c.OrderId && o.TicketId == ticketId))
            .ToListAsync();
        carts.Should().ContainSingle(c => c.Id == b.Value.CartItemId);
        carts.Should().OnlyContain(c => c.OrderId != send.Value.OrderId); // moved off the sent order

        // One SENT order (batch sent) + one DRAFT order (kept), correct batch numbering.
        var orders = await _ctx.Orders.Where(o => o.TicketId == ticketId)
            .OrderBy(o => o.OrderNumber).ToListAsync();
        orders.Should().HaveCount(2);
        orders[0].Status.Should().Be(OrderStatus.Sent);
        orders[1].Status.Should().Be(OrderStatus.Draft);
        orders[1].OrderNumber.Should().BeGreaterThan(orders[0].OrderNumber);

        // Only line A became an OrderItem.
        (await _ctx.OrderItems.CountAsync(oi => oi.TicketId == ticketId)).Should().Be(1);
    }

    [Fact]
    public async Task PartialSend_UnknownCartItem_Fails()
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None)).Value.TicketId;
        await Add().Handle(new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);

        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Rc(), Version())
            .Handle(new SendOrder.Command(ticketId, new long[] { 999999 }), CancellationToken.None);
        send.IsFailure.Should().BeTrue();
        send.Error.Code.Should().Be("Order.CartItemNotFound");
    }

    [Fact]
    public async Task Add_RemoveLeavesEmptyCart()
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None)).Value.TicketId;

        var add = await Add().Handle(
            new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);

        var rem = await new RemoveCartItem.Handler(_ctx, Staff(), Guard(), Cart(), Version())
            .Handle(new RemoveCartItem.Command(ticketId, add.Value.CartItemId), CancellationToken.None);
        rem.IsSuccess.Should().BeTrue();
        (await _ctx.CartItems.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Update_QtyZero_RemovesCartItem()
    {
        await AcquireLock();
        var ticketId = (await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, _shiftId, null), CancellationToken.None)).Value.TicketId;

        var add = await Add().Handle(
            new AddCartItem.Command(ticketId, _singleItemId, 1m, null, []), CancellationToken.None);

        // Update qty → 0 should remove the cart item.
        var upd = await new UpdateCartItem.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new UpdateCartItem.Command(ticketId, add.Value.CartItemId, 0m, null, null), CancellationToken.None);
        upd.IsSuccess.Should().BeTrue();
        upd.Value.CartItemId.Should().Be(0);
        upd.Value.LineTotal.Should().Be(0);
        (await _ctx.CartItems.AnyAsync()).Should().BeFalse();
    }

    // ---------- helpers ----------

    private AddCartItem.Handler Add() => new(
        _ctx, Staff(), Clock(), Guard(), new MenuPriceResolver(_ctx), Rc(), Cart(), Version());

    private async Task AcquireLock()
    {
        await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);
    }

    private TableOperationGuard Guard() => new(_ctx, Clock());
    private CartRecomputeService Cart() => new(_ctx, Rc(), Clock());
    private TicketRecomputeService TicketRecompute() => new(_ctx, Rc(), Clock());
    private static IVersionService Version() => Substitute.For<IVersionService>();

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
        foreach (var kv in RoundingKeys.Defaults) rc.GetDigits(kv.Key).Returns(kv.Value);
        return rc;
    }

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngân", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 5m, ServiceChargeVatPercent = 8m, CreatedAt = now, UpdatedAt = now };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var shift = new Shift { Code = "S1", Name = "Sáng", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uom = new Uom { Code = "phan", Name = "Phần", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item = new Item { Code = "PHO", Name = "Phở", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var item2 = new Item { Code = "BUN", Name = "Bún", BaseUom = uom, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var entry = new PriceEntry { PriceVariant = variant, Item = item, Price = 50000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var entry2 = new PriceEntry { PriceVariant = variant, Item = item2, Price = 40000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, counter, area, table, shift, uom, item, item2, priceTable, variant, entry, entry2);
        await _ctx.SaveChangesAsync();

        // drawer.OpenedByStaffAccountId references staff after it has an id
        drawer.OpenedByStaffAccountId = staff.Id;
        _ctx.Add(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id; _tableId = table.Id; _shiftId = shift.Id; _singleItemId = item.Id; _item2Id = item2.Id;
    }
}
