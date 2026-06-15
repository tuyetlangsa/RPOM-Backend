using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.Pricing;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.GetTicketDetails;

public static class GetTicketDetails
{
    public sealed record Query(long TicketId) : IQuery<Response>;

    public sealed record Response(
        Info Info,
        IReadOnlyList<ItemDetail> ItemDetails,
        IReadOnlyList<OrderBatch> OrderedItems,
        IReadOnlyList<OrderingItem> OrderingItems,
        PaymentSection Payment);

    public sealed record Info(
        long TicketId,
        string TicketCode,
        int TableId,
        string TableCode,
        int AreaId,
        string AreaName,
        int CounterId,
        string CounterName,
        short GuestCount,
        int? WaiterStaffId,
        string? WaiterName,
        int? ManagerStaffId,
        string? ManagerName,
        string Status,
        DateTime OpenedAt,
        int DurationMinutes,
        decimal ServiceChargePercent,
        decimal ServiceChargeVatPercent,
        int? DiscountPolicyId,
        string? DiscountPolicyName,
        decimal DiscountPercent,
        string? DiscountReason,
        decimal Subtotal,
        decimal LineDiscountTotal,
        decimal TicketDiscountTotal,
        decimal DiscountAmount,
        decimal ServiceChargeAmount,
        decimal VatAmount,
        decimal TotalAmount,
        decimal RoundingAdjustment,
        decimal PaidAmount,
        decimal RemainingAmount,
        decimal RefundAmount,
        bool HasEInvoiceRequest,
        bool HasGuestQrToken,
        int Version,
        DateTime UpdatedAt);

    public sealed record ItemDetail(
        long TicketItemSumId,
        int ItemId,
        string ItemCode,
        string ItemName,
        string UomCode,
        string UomName,
        decimal UnitPrice,
        decimal ChoicePricePerUnit,
        decimal TotalQuantity,
        decimal VatPercent,
        decimal ServiceChargePercent,
        decimal ServiceChargeVatPercent,
        decimal LineDiscountPercent,
        decimal TicketDiscountPercent,
        decimal TotalLineSubtotal,
        decimal TotalDiscount,
        decimal TotalServiceCharge,
        decimal TotalVat,
        decimal TotalAmount,
        decimal DisplayPrice,
        decimal DisplayLineTotal);

    public sealed record OrderBatch(
        long OrderId,
        short OrderNumber,
        string Status,
        DateTime? SentAt,
        int? CreatedByStaffId,
        string? CreatedByStaffName,
        string? Notes,
        IReadOnlyList<OrderedItem> Items);

    public sealed record OrderedItem(
        long OrderItemId,
        int ItemId,
        string ItemCode,
        string ItemName,
        string UomCode,
        string UomName,
        decimal Quantity,
        decimal UnitPrice,
        decimal ChoicePricePerUnit,
        decimal LineSubtotal,
        decimal LineTotal,
        string Status,
        int? KitchenStationId,
        string? KitchenStationName,
        DateTime SentAt,
        DateTime? StartCookAt,
        DateTime? ReadyAt,
        DateTime? DoneAt,
        int? CancellationReasonId,
        string? CancellationReasonName,
        string? CancellationNote,
        long? OriginalOrderItemId,
        string? Notes,
        IReadOnlyList<ItemComponent> Components);

    public sealed record OrderingItem(
        long CartItemId,
        long OrderId,
        int ItemId,
        string ItemCode,
        string ItemName,
        string UomCode,
        string UomName,
        decimal Quantity,
        decimal UnitPrice,
        decimal ChoicePricePerUnit,
        decimal LineSubtotal,
        decimal ServiceChargeAmount,
        decimal VatItemAmount,
        decimal VatScAmount,
        decimal VatAmount,
        decimal VatPercent,
        decimal LineTotal,
        long? OriginalOrderItemId,
        string? Notes,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        IReadOnlyList<ItemComponent> Components);

    public sealed record ItemComponent(
        int ItemId,
        string ItemName,
        string ComponentType,
        decimal Quantity,
        decimal ExtraPrice,
        string? Notes);

    public sealed record PaymentSection(
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        decimal RefundAmount,
        bool IsFullyPaid,
        bool HasPendingPayment,
        IReadOnlyList<PaymentLine> Payments);

    public sealed record PaymentLine(
        long TicketPaymentDetailId,
        int PaymentMethodId,
        string PaymentMethodCode,
        string PaymentMethodName,
        decimal Amount,
        string Status,
        DateTime? ProcessedAt,
        int ProcessedByStaffId,
        string ProcessedByStaffName,
        string? TransactionRef,
        string? Notes,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db, IDateTimeProvider clock, IRoundingConfig rc)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            long ticketId = request.TicketId;

            // --- Info ---
            // Project the raw scalar columns first; DurationMinutes is computed in memory
            // (clock difference) since (clock.UtcNow - OpenedAt).TotalMinutes does NOT
            // translate to SQL.
            var t = await db.Tickets
                .Where(x => x.Id == ticketId
                            && (x.Status == TicketStatus.Open || x.Status == TicketStatus.Closed))
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.TableId,
                    TableCode = x.Table.Code,
                    x.AreaId,
                    AreaName = x.Area.Name,
                    x.CounterId,
                    CounterName = x.Counter.Name,
                    x.GuestCount,
                    x.WaiterStaffId,
                    WaiterName = x.WaiterStaff != null ? x.WaiterStaff.FullName : null,
                    x.ManagerStaffId,
                    ManagerName = x.ManagerStaff != null ? x.ManagerStaff.FullName : null,
                    x.Status,
                    x.OpenedAt,
                    x.ServiceChargePercent,
                    x.ServiceChargeVatPercent,
                    x.DiscountPolicyId,
                    DiscountPolicyName = x.DiscountPolicy != null ? x.DiscountPolicy.Name : null,
                    x.DiscountPercent,
                    x.Subtotal,
                    x.LineDiscountTotal,
                    x.TicketDiscountTotal,
                    x.DiscountAmount,
                    x.ServiceChargeAmount,
                    x.VatAmount,
                    x.TotalAmount,
                    x.RoundingAdjustment,
                    x.PaidAmount,
                    x.RefundAmount,
                    HasEInvoiceRequest = db.EInvoices.Any(e => e.TicketId == x.Id),
                    HasGuestQrToken = x.GuestQrToken != null,
                    x.Version,
                    x.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);
            if (t is null)
            {
                return Result.Failure<Response>(TicketErrors.NotFound);
            }

            var info = new Info(
                t.Id, t.Code,
                t.TableId, t.TableCode, t.AreaId, t.AreaName,
                t.CounterId, t.CounterName,
                t.GuestCount,
                t.WaiterStaffId, t.WaiterName,
                t.ManagerStaffId, t.ManagerName,
                t.Status, t.OpenedAt, (int)(clock.UtcNow - t.OpenedAt).TotalMinutes,
                t.ServiceChargePercent, t.ServiceChargeVatPercent,
                t.DiscountPolicyId, t.DiscountPolicyName,
                t.DiscountPercent, null,
                t.Subtotal, t.LineDiscountTotal, t.TicketDiscountTotal,
                t.DiscountAmount, t.ServiceChargeAmount, t.VatAmount,
                t.TotalAmount, t.RoundingAdjustment,
                t.PaidAmount, t.TotalAmount - t.PaidAmount, t.RefundAmount,
                t.HasEInvoiceRequest, t.HasGuestQrToken,
                t.Version, t.UpdatedAt);

            // --- ItemDetails (bucket aggregate) ---
            List<TicketItemSum> sums = await db.TicketItemSums
                .Where(s => s.TicketId == ticketId)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync(ct);
            var itemDetails = sums.Select(s =>
            {
                decimal display = Money.Round(
                    s.UnitPrice * (1 + s.VatPercent / 100m), rc, RoundingKeys.MenuDisplay);
                return new ItemDetail(
                    s.Id, s.ItemId, s.ItemCode, s.ItemName, s.UomCode, s.UomName,
                    s.UnitPrice, s.ChoicePricePerUnit, s.TotalQuantity,
                    s.VatPercent, s.ServiceChargePercent, s.ServiceChargeVatPercent,
                    s.LineDiscountPercent, s.TicketDiscountPercent,
                    s.TotalLineSubtotal, s.TotalDiscount, s.TotalServiceCharge,
                    s.TotalVat, s.TotalAmount,
                    display, s.TotalQuantity * display);
            }).ToList();

            // --- Ordered (Order Status != DRAFT) ---
            var orderRows = await db.Orders
                .Where(o => o.TicketId == ticketId && o.Status != OrderStatus.Draft)
                .OrderBy(o => o.OrderNumber)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Status,
                    o.SentAt,
                    o.CreatedByStaffId,
                    CreatedByStaffName = o.CreatedByStaff != null ? o.CreatedByStaff.FullName : null,
                    o.Notes
                })
                .ToListAsync(ct);
            var orderIds = orderRows.Select(o => o.Id).ToList();

            var orderItems = await db.OrderItems
                .Where(oi => orderIds.Contains(oi.OrderId))
                .OrderBy(oi => oi.SentAt)
                .Select(oi => new
                {
                    oi.Id,
                    oi.OrderId,
                    oi.ItemId,
                    oi.ItemCode,
                    oi.ItemName,
                    oi.UomCode,
                    oi.UomName,
                    oi.Quantity,
                    oi.UnitPrice,
                    oi.ChoicePricePerUnit,
                    oi.LineSubtotal,
                    oi.LineTotal,
                    oi.Status,
                    oi.KitchenStationId,
                    KitchenStationName = oi.KitchenStation != null ? oi.KitchenStation.Name : null,
                    oi.SentAt,
                    oi.StartCookAt,
                    oi.ReadyAt,
                    oi.DoneAt,
                    oi.CancellationReasonId,
                    CancellationReasonName = oi.CancellationReason != null ? oi.CancellationReason.Name : null,
                    oi.CancellationNote,
                    oi.OriginalOrderItemId,
                    oi.Notes
                })
                .ToListAsync(ct);
            var orderItemIds = orderItems.Select(x => x.Id).ToList();

            var orderItemDetails = await db.OrderItemDetails
                .Where(d => orderItemIds.Contains(d.OrderItemId))
                .Select(d => new
                { d.OrderItemId, d.ItemId, d.ItemName, d.ComponentType, d.Quantity, d.ExtraPrice, d.Notes })
                .ToListAsync(ct);
            var oiDetailsByItem = orderItemDetails.GroupBy(d => d.OrderItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var orderedItemsByOrder = orderItems.GroupBy(x => x.OrderId)
                .ToDictionary(g => g.Key, g => g.Select(oi => new OrderedItem(
                        oi.Id, oi.ItemId, oi.ItemCode, oi.ItemName, oi.UomCode, oi.UomName,
                        oi.Quantity, oi.UnitPrice, oi.ChoicePricePerUnit, oi.LineSubtotal, oi.LineTotal,
                        oi.Status, oi.KitchenStationId, oi.KitchenStationName,
                        oi.SentAt, oi.StartCookAt, oi.ReadyAt, oi.DoneAt,
                        oi.CancellationReasonId, oi.CancellationReasonName, oi.CancellationNote, oi.OriginalOrderItemId, oi.Notes,
                        (oiDetailsByItem.GetValueOrDefault(oi.Id) ?? new()).Select(d => new ItemComponent(
                            d.ItemId, d.ItemName, d.ComponentType, d.Quantity, d.ExtraPrice, d.Notes)).ToList()))
                    .ToList());

            var orderBatches = orderRows.Select(o => new OrderBatch(
                o.Id, o.OrderNumber, o.Status, o.SentAt, o.CreatedByStaffId, o.CreatedByStaffName, o.Notes,
                orderedItemsByOrder.GetValueOrDefault(o.Id) ?? new List<OrderedItem>())).ToList();

            // --- Ordering (Order Status == DRAFT) ---
            List<long> draftOrderIds = await db.Orders
                .Where(o => o.TicketId == ticketId && o.Status == OrderStatus.Draft)
                .Select(o => o.Id).ToListAsync(ct);

            var cartItems = await db.CartItems
                .Where(c => draftOrderIds.Contains(c.OrderId))
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.OrderId,
                    c.ItemId,
                    c.ItemCode,
                    c.ItemName,
                    c.UomCode,
                    c.UomName,
                    c.Quantity,
                    c.UnitPrice,
                    c.ChoicePricePerUnit,
                    c.LineSubtotal,
                    c.ServiceChargeAmount,
                    c.VatItemAmount,
                    c.VatScAmount,
                    c.VatAmount,
                    c.VatPercent,
                    c.LineTotal,
                    c.OriginalOrderItemId,
                    c.Notes,
                    c.CreatedAt,
                    c.UpdatedAt
                })
                .ToListAsync(ct);
            var cartItemIds = cartItems.Select(c => c.Id).ToList();

            var cartItemDetails = await db.CartItemDetails
                .Where(d => cartItemIds.Contains(d.CartItemId))
                .Select(d => new
                { d.CartItemId, d.ItemId, d.ItemName, d.ComponentType, d.Quantity, d.ExtraPrice, d.Notes })
                .ToListAsync(ct);
            var ciDetailsByItem = cartItemDetails.GroupBy(d => d.CartItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var orderingItems = cartItems.Select(c => new OrderingItem(
                    c.Id, c.OrderId, c.ItemId, c.ItemCode, c.ItemName, c.UomCode, c.UomName,
                    c.Quantity, c.UnitPrice, c.ChoicePricePerUnit, c.LineSubtotal,
                    c.ServiceChargeAmount, c.VatItemAmount, c.VatScAmount, c.VatAmount, c.VatPercent, c.LineTotal,
                    c.OriginalOrderItemId, c.Notes, c.CreatedAt, c.UpdatedAt,
                    (ciDetailsByItem.GetValueOrDefault(c.Id) ?? new()).Select(d => new ItemComponent(
                        d.ItemId, d.ItemName, d.ComponentType, d.Quantity, d.ExtraPrice, d.Notes)).ToList()))
                .ToList();

            // --- Payment ---
            List<PaymentLine> payments = await db.TicketPaymentDetails
                .Where(p => p.TicketId == ticketId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new PaymentLine(
                    p.Id, p.PaymentMethodId, p.PaymentMethod.Code, p.PaymentMethod.Name,
                    p.Amount, p.Status, p.ProcessedAt, p.ProcessedByStaffId,
                    p.ProcessedByStaff.FullName, p.TransactionRef, p.Notes, p.CreatedAt, p.UpdatedAt))
                .ToListAsync(ct);

            var paymentSection = new PaymentSection(
                info.TotalAmount, info.PaidAmount, info.RemainingAmount, info.RefundAmount,
                info.PaidAmount >= info.TotalAmount,
                payments.Any(p => p.Status == TicketPaymentStatus.Pending),
                payments);

            return Result.Success(new Response(
                info, itemDetails, orderBatches, orderingItems, paymentSection));
        }
    }
}
