using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Configuration;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Application.Abstraction.Tables;
using Rpom.Application.Abstraction.User;
using Rpom.Application.Abstraction.Versioning;
using Rpom.Application.Cashier.AcquireTableLock;
using Rpom.Application.Cashier.AddCartItem;
using Rpom.Application.Cashier.AddRefundLine;
using Rpom.Application.Cashier.CancelOrderItem;
using Rpom.Application.Cashier.CancelTicket;
using Rpom.Application.Cashier.MarkDoneOrderItem;
using Rpom.Application.Cashier.MarkReadyOrderItem;
using Rpom.Application.Cashier.OpenTicket;
using Rpom.Application.Cashier.SendOrder;
using Rpom.Application.Cashier.StartCookOrderItem;
using Rpom.Domain.Access;
using Rpom.Domain.Audit;
using Rpom.Domain.Common;
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

public sealed class PricingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db =
        new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg17").Build();
    private ApplicationDbContext _ctx = null!;
    private int _staffId, _tableId, _shiftId;
    private int _managerId, _reasonActiveId, _reasonInactiveId;
    private int _phoId, _biaId, _cocaId, _traDaId, _caPheId;

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
    public async Task VatExcludedItem_HasCorrectPricing()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var add = await AddItem(ticket, _phoId, 2);
        add.IsSuccess.Should().BeTrue();
        add.Value.LineTotal.Should().Be(108_000m);
    }

    [Fact]
    public async Task VatIncludedItem_HasCorrectPricing()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var add = await AddItem(ticket, _traDaId, 1);
        add.IsSuccess.Should().BeTrue();
        add.Value.LineTotal.Should().Be(5_000m);
    }

    [Fact]
    public async Task VatIncludedItem_MixedWithExcluded_SendOrder_TotalsMatch()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 2);
        await AddItem(ticket, _traDaId, 1);
        var send = await Send(ticket);
        // Pho: 2x50000=100000 +8%VAT=8000=108000; TraDa: 5000; Total=113000
        send.Value.TotalAmount.Should().Be(113_000m);
    }

    [Fact]
    public async Task DiscountPercent_AutoApply_BillAboveThreshold()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 6; i++) await AddItem(ticket, _phoId, 1);
        var send = await Send(ticket);
        // Subtotal=300000, Discount 10%=30000, Taxable=270000, VAT 8%=21600, Total=291600
        send.Value.TotalAmount.Should().Be(291_600m);
    }

    [Fact]
    public async Task DiscountFixed_AutoApply_BillAboveThreshold()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 20; i++) await AddItem(ticket, _phoId, 1);
        var send = await Send(ticket);
        // Subtotal=1000000, Discount=-100000, 900000+8%VAT=72000, Total=972000
        send.Value.TotalAmount.Should().Be(972_000m);
    }

    [Fact]
    public async Task FixedDiscount_ReDerivesPercent_KeepsAmountAsBillGrows()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 20; i++) await AddItem(ticket, _phoId, 1);   // 20 * 50k = 1,000,000
        var send = await Send(ticket);
        send.Value.TotalAmount.Should().BeApproximately(972_000m, 1m);

        for (int i = 0; i < 4; i++) await AddItem(ticket, _phoId, 1);
        await Send(ticket);
        var t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticket);
        t.DiscountAmount.Should().BeApproximately(100_000m, 1m);
    }

    [Fact]
    public async Task FixedDiscount_RemovedWhenSubtotalDropsBelowThreshold()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        for (int i = 0; i < 20; i++) await AddItem(ticket, _phoId, 1);   // 1,000,000
        await Send(ticket);

        var item = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket && o.Status == OrderItemStatus.Pending);
        await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new CancelOrderItem.Command(ticket, new List<long> { item.Id }), CancellationToken.None);

        var t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticket);
        t.DiscountAmount.Should().Be(0m);
        t.DiscountPolicyId.Should().BeNull();
    }

    [Fact]
    public async Task CancelOrderItem_RecomputesTicket()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        // Add 3 Pho items as distinct lines (distinct notes prevent the note-free merge) to get
        // 3 OrderItems. Notes don't affect price.
        await AddItem(ticket, _phoId, 1, "bàn 1");
        await AddItem(ticket, _phoId, 1, "bàn 2");
        await AddItem(ticket, _phoId, 1, "bàn 3");
        var send = await Send(ticket);
        // 3 * 50000 = 150000 + 8% VAT = 162000
        send.Value.TotalAmount.Should().Be(162_000m);

        var items = await _ctx.OrderItems
            .Where(oi => oi.TicketId == ticket && oi.Status == OrderItemStatus.Pending)
            .ToListAsync();
        items.Count.Should().Be(3);

        // Cancel 1 item.
        var cancel = await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new CancelOrderItem.Command(ticket, new List<long> { items[0].Id }), CancellationToken.None);
        cancel.IsSuccess.Should().BeTrue();

        // Remaining 2 items: 100000 + 8% VAT = 108000
        var final = await _ctx.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticket);
        final.TotalAmount.Should().Be(108_000m);
    }

    [Fact]
    public async Task CancelAllItems_OneByOne_BumpsParentOrderToDone()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 1, "1");
        await AddItem(ticket, _phoId, 1, "2");
        await AddItem(ticket, _phoId, 1, "3");
        await Send(ticket);

        var items = await _ctx.OrderItems
            .Where(oi => oi.TicketId == ticket && oi.Status == OrderItemStatus.Pending)
            .ToListAsync();
        items.Count.Should().Be(3);
        long orderId = items[0].OrderId;

        // Cancel one at a time (separate handler calls, each its own SaveChanges).
        foreach (var it in items)
        {
            var c = await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
                .Handle(new CancelOrderItem.Command(ticket, new List<long> { it.Id }), CancellationToken.None);
            c.IsSuccess.Should().BeTrue();
        }

        // All items terminal → parent order must roll up to DONE.
        var order = await _ctx.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.Done);
    }

    [Fact]
    public async Task CancelMultiOrderBatch_BumpsOnlyFullyCancelledOrder()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        // Order A: one item.
        await AddItem(ticket, _phoId, 1, "a1");
        await Send(ticket);
        // Order B: two items.
        await AddItem(ticket, _phoId, 1, "b1");
        await AddItem(ticket, _phoId, 1, "b2");
        await Send(ticket);

        var orders = await _ctx.Orders.AsNoTracking()
            .Where(o => o.TicketId == ticket && o.Status != OrderStatus.Draft)
            .OrderBy(o => o.OrderNumber).ToListAsync();
        orders.Count.Should().Be(2);
        long orderA = orders[0].Id, orderB = orders[1].Id;

        var itemA = await _ctx.OrderItems.AsNoTracking().FirstAsync(oi => oi.OrderId == orderA);
        var itemsB = await _ctx.OrderItems.AsNoTracking().Where(oi => oi.OrderId == orderB).OrderBy(oi => oi.Id).ToListAsync();

        // One batch: cancel all of A + only the first item of B.
        var c = await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new CancelOrderItem.Command(ticket, new List<long> { itemA.Id, itemsB[0].Id }), CancellationToken.None);
        c.IsSuccess.Should().BeTrue();

        var a = await _ctx.Orders.AsNoTracking().FirstAsync(o => o.Id == orderA);
        var b = await _ctx.Orders.AsNoTracking().FirstAsync(o => o.Id == orderB);
        a.Status.Should().Be(OrderStatus.Done);   // all its items cancelled
        b.Status.Should().Be(OrderStatus.Sent);   // still has one PENDING item
    }

    [Fact]
    public async Task MarkDoneAllItems_OneByOne_BumpsParentOrderToDone()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 1, "1");
        await AddItem(ticket, _phoId, 1, "2");
        await Send(ticket);

        var items = await _ctx.OrderItems
            .Where(oi => oi.TicketId == ticket && oi.Status == OrderItemStatus.Pending)
            .ToListAsync();
        items.Count.Should().Be(2);
        long orderId = items[0].OrderId;
        var ids = items.Select(i => i.Id).ToList();

        await new StartCookOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new StartCookOrderItem.Command(ticket, ids), CancellationToken.None);
        await new MarkReadyOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new MarkReadyOrderItem.Command(ticket, ids), CancellationToken.None);

        // Mark done one at a time.
        foreach (var id in ids)
        {
            var d = await new MarkDoneOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
                .Handle(new MarkDoneOrderItem.Command(ticket, new List<long> { id }), CancellationToken.None);
            d.IsSuccess.Should().BeTrue();
        }

        var order = await _ctx.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.Done);
    }

    [Fact]
    public async Task PartialSend_KeptItemsInNewDraft()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        var a = await AddItem(ticket, _phoId, 2);
        var b = await AddItem(ticket, _cocaId, 1);

        var send = await new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new SendOrder.Command(ticket, new List<long> { a.Value.CartItemId }), CancellationToken.None);
        send.IsSuccess.Should().BeTrue();

        var cart = await _ctx.CartItems.CountAsync(ci =>
            _ctx.Orders.Any(o => o.Id == ci.OrderId && o.TicketId == ticket && o.Status == OrderStatus.Draft));
        cart.Should().Be(1);
    }

    // ─── Cancel Ticket ──────────────────────────────────────────────────────

    [Fact]
    public async Task CancelEmptyTicket_SetsCancelledReleasesLockAndAudits()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, "mo nham");
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(TicketStatus.Cancelled);

        var t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticket);
        t.Status.Should().Be(TicketStatus.Cancelled);
        t.CancelledAt.Should().NotBeNull();
        t.CancellationReasonId.Should().Be(_reasonActiveId);
        t.ManagerStaffId.Should().Be(_managerId);

        (await _ctx.TableLocks.AnyAsync(l => l.TableId == _tableId)).Should().BeFalse();
        (await _ctx.AuditLogs.AnyAsync(a =>
            a.EntityType == nameof(Ticket) && a.EntityId == ticket && a.Action == "CANCEL")).Should().BeTrue();
    }

    [Fact]
    public async Task CancelTicket_WithActiveOrderItem_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 1);
        await Send(ticket);

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.HasActiveItems");

        (await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticket)).Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task CancelTicket_AfterAllItemsCancelled_Succeeds()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 1);
        await Send(ticket);
        var item = await _ctx.OrderItems.FirstAsync(oi => oi.TicketId == ticket);
        await new CancelOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new CancelOrderItem.Command(ticket, new List<long> { item.Id }), CancellationToken.None);

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, null);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelTicket_WithPendingPayment_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddPayment(ticket, TicketPaymentStatus.Pending);

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.HasPendingPayment");
    }

    [Fact]
    public async Task CancelTicket_WithSuccessPayment_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddPayment(ticket, TicketPaymentStatus.Success);

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.HasSuccessfulPayment");
    }

    [Fact]
    public async Task CancelTicket_DropsDraftCart()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 1);   // stays in DRAFT cart (not sent)

        var r = await CancelTicketCall(ticket, _managerId, _reasonActiveId, null);
        r.IsSuccess.Should().BeTrue();

        var draftOrderIds = await _ctx.Orders.AsNoTracking()
            .Where(o => o.TicketId == ticket && o.Status == OrderStatus.Draft).Select(o => o.Id).ToListAsync();
        (await _ctx.CartItems.AnyAsync(c => draftOrderIds.Contains(c.OrderId))).Should().BeFalse();
    }

    [Fact]
    public async Task CancelTicket_NonManager_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        // _staffId is the cashier (role CASHIER), not a manager.
        var r = await CancelTicketCall(ticket, _staffId, _reasonActiveId, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.InvalidManager");
    }

    [Fact]
    public async Task CancelTicket_InactiveReason_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();

        var r = await CancelTicketCall(ticket, _managerId, _reasonInactiveId, null);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.InvalidCancellationReason");
    }

    private Task<Result<CancelTicket.Response>> CancelTicketCall(long ticketId, int managerId, int reasonId, string? note)
    {
        return new CancelTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new CancelTicket.Command(ticketId, managerId, reasonId, note), CancellationToken.None);
    }

    private async Task AddPayment(long ticketId, string status)
    {
        var pm = await _ctx.PaymentMethods.FirstOrDefaultAsync();
        if (pm is null)
        {
            pm = new PaymentMethod { Code = "CASH", Name = "Tien mat", DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _ctx.PaymentMethods.Add(pm);
            await _ctx.SaveChangesAsync();
        }
        _ctx.TicketPaymentDetails.Add(new TicketPaymentDetail
        {
            TicketId = ticketId, PaymentMethodId = pm.Id, Amount = 10_000m, Status = status,
            ProcessedByStaffId = _staffId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task AcquireLock()
    {
        var r = await new AcquireTableLock.Handler(_ctx, Staff(), Clock(), Version(), Config())
            .Handle(new AcquireTableLock.Command(_tableId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
    }

    private async Task<long> OpenTicket()
    {
        var r = await new OpenTicket.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new OpenTicket.Command(_tableId, 2, null), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        return r.Value.TicketId;
    }

    private Task<Result<AddCartItem.Response>> AddItem(long ticketId, int itemId, decimal qty, string? notes = null)
    {
        return new AddCartItem.Handler(_ctx, Staff(), Clock(), Guard(), PriceResolver(), Rc(), Cart(), Version())
            .Handle(new AddCartItem.Command(ticketId, itemId, qty, notes, []), CancellationToken.None);
    }

    private Task<Result<SendOrder.Response>> Send(long ticketId)
    {
        return new SendOrder.Handler(_ctx, Staff(), Clock(), Guard(), TicketRecompute(), Version())
            .Handle(new SendOrder.Command(ticketId, null), CancellationToken.None);
    }

    [Fact]
    public async Task AddRefundLine_CreatesNegativeDraftCartLine_LinkedToOriginal()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 3);
        await Send(ticket);
        var original = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket);
        await new StartCookOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new StartCookOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);
        await new MarkReadyOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new MarkReadyOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);
        await new MarkDoneOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new MarkDoneOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);

        var r = await new AddRefundLine.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new AddRefundLine.Command(ticket, original.Id, 1, _reasonActiveId, "vỡ"), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();

        var draft = await _ctx.CartItems.AsNoTracking().FirstAsync(c => c.OriginalOrderItemId == original.Id);
        draft.Quantity.Should().Be(-1m);
        draft.ItemId.Should().Be(original.ItemId);
        draft.UnitPrice.Should().Be(original.UnitPrice);
        draft.CancellationReasonId.Should().Be(_reasonActiveId);
    }

    [Fact]
    public async Task AddRefundLine_PendingOriginal_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 2);
        await Send(ticket);
        var original = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket); // PENDING

        var r = await new AddRefundLine.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new AddRefundLine.Command(ticket, original.Id, 1, _reasonActiveId, null), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("OrderItem.NotRefundable");
    }

    [Fact]
    public async Task AddRefundLine_ExceedsRemaining_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 2);
        await Send(ticket);
        var original = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket);
        await new StartCookOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new StartCookOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);

        var r = await new AddRefundLine.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new AddRefundLine.Command(ticket, original.Id, 3, _reasonActiveId, null), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("OrderItem.RefundQuantityExceeded");
    }

    [Fact]
    public async Task AddRefundLine_InactiveReason_Fails()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 2);
        await Send(ticket);
        var original = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket);
        await new StartCookOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new StartCookOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);

        var r = await new AddRefundLine.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new AddRefundLine.Command(ticket, original.Id, 1, _reasonInactiveId, null), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Ticket.InvalidCancellationReason");
    }

    [Fact]
    public async Task SendOrder_MaterializesRefundLine_LinkedAndAudited_CreditsBill()
    {
        await AcquireLock();
        var ticket = await OpenTicket();
        await AddItem(ticket, _phoId, 3);            // 3 pho = 150,000
        await Send(ticket);
        var original = await _ctx.OrderItems.FirstAsync(o => o.TicketId == ticket);
        await new StartCookOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new StartCookOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);
        await new MarkReadyOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new MarkReadyOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);
        await new MarkDoneOrderItem.Handler(_ctx, Staff(), Clock(), Guard(), Version())
            .Handle(new MarkDoneOrderItem.Command(ticket, new List<long> { original.Id }), CancellationToken.None);

        await new AddRefundLine.Handler(_ctx, Staff(), Clock(), Guard(), Cart(), Version())
            .Handle(new AddRefundLine.Command(ticket, original.Id, 1, _reasonActiveId, "vỡ"), CancellationToken.None);
        await Send(ticket);   // materialize the refund line

        var refundRow = await _ctx.OrderItems.AsNoTracking().FirstAsync(o => o.OriginalOrderItemId == original.Id);
        refundRow.Quantity.Should().Be(-1m);
        refundRow.Status.Should().Be(OrderItemStatus.Pending);
        refundRow.CancellationReasonId.Should().Be(_reasonActiveId);

        (await _ctx.AuditLogs.AnyAsync(a =>
            a.EntityType == nameof(OrderItem) && a.EntityId == original.Id && a.Action == "REFUND"))
            .Should().BeTrue();

        // Bill: net 2 pho = 100,000 + 8% VAT = 108,000.
        var t = await _ctx.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticket);
        t.TotalAmount.Should().Be(108_000m);
    }

    private ICurrentStaff Staff() => CreateStaff.Staff(_staffId);
    private IDateTimeProvider Clock() => CreateStaff.Clock();
    private ITableOperationGuard Guard() => new TableOperationGuard(_ctx, Clock(), Config());
    private IVersionService Version() => Substitute.For<IVersionService>();

    // Null config → typed accessors fall back to defaults (TTL = 60s).
    private IConfigValueService Config()
    {
        var c = Substitute.For<IConfigValueService>();
        c.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        return c;
    }
    private IRoundingConfig Rc() => CreateStaff.RoundingConfig();
    private ITicketRecomputeService TicketRecompute() => new TicketRecomputeService(_ctx, Rc(), Clock());
    private ICartRecomputeService Cart() => new CartRecomputeService(_ctx, Rc(), Clock());
    private IMenuPriceResolver PriceResolver() => new MenuPriceResolver(_ctx);

    private async Task SeedAsync()
    {
        var now = DateTime.UtcNow;
        var role = new Role { Code = "CASHIER", Name = "Cashier", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var staff = new StaffAccount { Username = "c", PasswordHash = "x", FullName = "Thu ngan", Role = role, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var mgrRole = new Role { Code = "MANAGER", Name = "Manager", IsSystemRole = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var manager = new StaffAccount { Username = "m", PasswordHash = "x", FullName = "Quan ly", Role = mgrRole, IsActive = true, IsLocked = false, CreatedAt = now, UpdatedAt = now };
        var reasonActive = new CancellationReason { Code = "CUS_CHANGE_MIND", Name = "Khach doi y", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var reasonInactive = new CancellationReason { Code = "OLD", Name = "Ly do cu", DisplayOrder = 2, IsActive = false, CreatedAt = now, UpdatedAt = now };
        var counter = new Counter { Name = "C", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var area = new Area { Counter = counter, Name = "A", DisplayOrder = 1, IsActive = true, ServiceChargePercent = 0m, ServiceChargeVatPercent = 0m, CreatedAt = now, UpdatedAt = now };
        var table = new Table { Area = area, Code = "T01", SeatCount = 4, Status = TableStatus.Available, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var shift = new Shift { Code = "S1", Name = "Sang", BeginTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59), IsActive = true, CreatedAt = now, UpdatedAt = now };
        var drawer = new CashDrawerSession { Counter = counter, Shift = shift, OpenedByStaffAccountId = 0, Status = CashDrawerStatus.Open, OpeningCash = 0m, OpenedAt = now, CreatedAt = now, UpdatedAt = now };

        var uomPhan = new Uom { Code = "phan", Name = "Phan", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var uomLon = new Uom { Code = "lon", Name = "Lon", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var uomLy = new Uom { Code = "ly", Name = "Ly", IsActive = true, CreatedAt = now, UpdatedAt = now };

        var pho = new Item { Code = "PHO", Name = "Pho", BaseUom = uomPhan, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var bia = new Item { Code = "BIA", Name = "Bia", BaseUom = uomLon, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var coca = new Item { Code = "COCA", Name = "Coca", BaseUom = uomLon, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var traDa = new Item { Code = "TRADA", Name = "Tra da", BaseUom = uomLy, VatPercent = 10m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var caPhe = new Item { Code = "CAPHE", Name = "Ca phe", BaseUom = uomLy, VatPercent = 8m, IsStockable = false, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var priceTable = new PriceTable { Code = "PT", Name = "Default", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var variant = new PriceVariant { PriceTable = priceTable, Code = "PV", Name = "Base", AppliesToAllAreas = true, IsActive = true, CreatedAt = now, UpdatedAt = now };

        var pePho = new PriceEntry { PriceVariant = variant, Item = pho, Price = 50_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peBia = new PriceEntry { PriceVariant = variant, Item = bia, Price = 22_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peCoca = new PriceEntry { PriceVariant = variant, Item = coca, Price = 15_000m, IsVatIncluded = false, CreatedAt = now, UpdatedAt = now };
        var peTraDa = new PriceEntry { PriceVariant = variant, Item = traDa, Price = 5_000m, IsVatIncluded = true, CreatedAt = now, UpdatedAt = now };
        var peCaPhe = new PriceEntry { PriceVariant = variant, Item = caPhe, Price = 20_000m, IsVatIncluded = true, CreatedAt = now, UpdatedAt = now };

        var dp1 = new DiscountPolicy { Code = "GIAM10", Name = "Bill 200k giam 10%", DiscountType = DiscountType.TicketThreshold, IsAutoApply = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dp1.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp1, ThresholdAmount = 200_000m, ApplyType = DiscountApplyType.Percent, DiscountValue = 10m, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });
        dp1.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp1, ThresholdAmount = 500_000m, ApplyType = DiscountApplyType.Percent, DiscountValue = 15m, DisplayOrder = 2, CreatedAt = now, UpdatedAt = now });

        var dp2 = new DiscountPolicy { Code = "GIAM100", Name = "Bill 1M giam 100k", DiscountType = DiscountType.TicketThreshold, IsAutoApply = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dp2.Conditions.Add(new DiscountPolicyCondition { DiscountPolicy = dp2, ThresholdAmount = 1_000_000m, ApplyType = DiscountApplyType.Fixed, DiscountValue = 100_000m, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });

        var cat = new Category { Code = "HANG_BAN", Name = "Hang ban", Level = 0, Path = "1;", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };
        var catSub = new Category { Code = "DOUONG", Name = "Do uong", Parent = cat, Level = 1, Path = "1;999;", DisplayOrder = 1, IsActive = true, CreatedAt = now, UpdatedAt = now };

        _ctx.AddRange(role, staff, mgrRole, manager, reasonActive, reasonInactive, counter, area, table, shift,
            uomPhan, uomLon, uomLy, pho, bia, coca, traDa, caPhe,
            priceTable, variant, pePho, peBia, peCoca, peTraDa, peCaPhe, dp1, dp2);
        await _ctx.SaveChangesAsync();
        cat.Path = $"1;{cat.Id};"; catSub.Path = $"1;{cat.Id};{catSub.Id};";
        await _ctx.SaveChangesAsync();

        _ctx.ItemCategories.AddRange(
            new ItemCategory { Item = pho, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = bia, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = coca, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = traDa, Category = catSub, IsMain = true, CreatedAt = now },
            new ItemCategory { Item = caPhe, Category = catSub, IsMain = true, CreatedAt = now }
        );
        _ctx.AreaMenuCategories.Add(new AreaMenuCategory { Area = area, Category = catSub, CreatedAt = now });

        drawer.OpenedByStaffAccountId = staff.Id; drawer.ShiftId = shift.Id;
        _ctx.AddRange(drawer);
        await _ctx.SaveChangesAsync();

        _staffId = staff.Id; _tableId = table.Id; _shiftId = shift.Id;
        _managerId = manager.Id; _reasonActiveId = reasonActive.Id; _reasonInactiveId = reasonInactive.Id;
        _phoId = pho.Id; _biaId = bia.Id; _cocaId = coca.Id; _traDaId = traDa.Id; _caPheId = caPhe.Id;
    }
}

internal static class CreateStaff
{
    public static ICurrentStaff Staff(int id)
    {
        var s = Substitute.For<ICurrentStaff>();
        s.StaffAccountId.Returns(id);
        return s;
    }
    public static IDateTimeProvider Clock()
    {
        var c = Substitute.For<IDateTimeProvider>();
        c.UtcNow.Returns(new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));
        return c;
    }
    public static IRoundingConfig RoundingConfig()
    {
        var rc = Substitute.For<IRoundingConfig>();
        rc.GetDigits(Arg.Any<string>()).Returns(2);
        return rc;
    }
}
