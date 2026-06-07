using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Clock;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;
using Rpom.Domain.Sales;

namespace Rpom.Application.Cashier.GetTicketsOnTable;

public static class GetTicketsOnTable
{
    public sealed record Query(int TableId) : IQuery<Response>;

    public sealed record Response(
        int TableId,
        string TableCode,
        int AreaId,
        string AreaName,
        IReadOnlyList<TicketDto> Tickets);

    public sealed record TicketDto(
        long TicketId,
        string TicketCode,
        short GuestCount,
        int? WaiterStaffId,
        string? WaiterName,
        DateTime OpenedAt,
        int DurationMinutes,
        int OrderItemCount,
        int PendingOrderItemCount,
        int CartItemCount,
        decimal Subtotal,
        decimal DiscountAmount,
        decimal ServiceChargeAmount,
        decimal VatAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        bool HasUnsentItems,
        bool HasPendingPayment,
        bool HasGuestQrToken,
        string Status);

    internal sealed class Handler(IDbContext db, IDateTimeProvider clock)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var table = await db.Tables
                .Where(t => t.Id == request.TableId)
                .Select(t => new { t.Id, t.Code, t.AreaId, AreaName = t.Area.Name })
                .FirstOrDefaultAsync(ct);
            if (table is null) return Result.Failure<Response>(TableErrors.NotFound);

            var now = clock.UtcNow;

            // 1. Load open tickets on this table as a lightweight intermediate list (scalar
            //    columns + waiter name). Deeply-nested correlated subqueries are avoided here;
            //    the per-ticket counts/flags are computed via separate aggregate queries below.
            var tickets = await db.Tickets
                .Where(tk => tk.TableId == table.Id && tk.Status == TicketStatus.Open)
                .OrderByDescending(tk => tk.OpenedAt)
                .Select(tk => new
                {
                    tk.Id,
                    tk.Code,
                    tk.GuestCount,
                    tk.WaiterStaffId,
                    WaiterName = tk.WaiterStaff != null ? tk.WaiterStaff.FullName : null,
                    tk.OpenedAt,
                    tk.Subtotal,
                    tk.DiscountAmount,
                    tk.ServiceChargeAmount,
                    tk.VatAmount,
                    tk.TotalAmount,
                    tk.PaidAmount,
                    HasGuestQrToken = tk.GuestQrToken != null,
                    tk.Status,
                })
                .ToListAsync(ct);
            var ticketIds = tickets.Select(t => t.Id).ToList();

            if (ticketIds.Count == 0)
            {
                return Result.Success(new Response(
                    table.Id, table.Code, table.AreaId, table.AreaName, []));
            }

            // 2. Order-item counts keyed by ticket id (OrderItem.TicketId is denormalized).
            var orderItemCounts = await db.OrderItems
                .Where(oi => ticketIds.Contains(oi.TicketId)
                    && oi.Status != OrderItemStatus.Cancelled)
                .GroupBy(oi => oi.TicketId)
                .Select(g => new { TicketId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TicketId, x => x.Count, ct);

            var pendingOrderItemCounts = await db.OrderItems
                .Where(oi => ticketIds.Contains(oi.TicketId)
                    && oi.Status == OrderItemStatus.Pending)
                .GroupBy(oi => oi.TicketId)
                .Select(g => new { TicketId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TicketId, x => x.Count, ct);

            // 3. Cart-item counts keyed by ticket id (CartItem → Order(DRAFT) → Ticket).
            var cartItemCounts = await db.CartItems
                .Join(db.Orders, ci => ci.OrderId, o => o.Id, (ci, o) => new { ci, o })
                .Where(x => ticketIds.Contains(x.o.TicketId) && x.o.Status == OrderStatus.Draft)
                .GroupBy(x => x.o.TicketId)
                .Select(g => new { TicketId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TicketId, x => x.Count, ct);

            // 4. Pending-payment flags keyed by ticket id.
            var pendingPaymentTicketIds = await db.TicketPaymentDetails
                .Where(p => ticketIds.Contains(p.TicketId)
                    && p.Status == TicketPaymentStatus.Pending)
                .Select(p => p.TicketId)
                .Distinct()
                .ToListAsync(ct);
            var pendingPaymentSet = pendingPaymentTicketIds.ToHashSet();

            // 5. Assemble the TicketDto list in memory.
            var ticketDtos = tickets.Select(t =>
            {
                var cartCount = cartItemCounts.GetValueOrDefault(t.Id);
                return new TicketDto(
                    t.Id,
                    t.Code,
                    t.GuestCount,
                    t.WaiterStaffId,
                    t.WaiterName,
                    t.OpenedAt,
                    (int)(now - t.OpenedAt).TotalMinutes,
                    orderItemCounts.GetValueOrDefault(t.Id),
                    pendingOrderItemCounts.GetValueOrDefault(t.Id),
                    cartCount,
                    t.Subtotal,
                    t.DiscountAmount,
                    t.ServiceChargeAmount,
                    t.VatAmount,
                    t.TotalAmount,
                    t.PaidAmount,
                    t.TotalAmount - t.PaidAmount,
                    cartCount > 0,
                    pendingPaymentSet.Contains(t.Id),
                    t.HasGuestQrToken,
                    t.Status);
            }).ToList();

            return Result.Success(new Response(
                table.Id, table.Code, table.AreaId, table.AreaName, ticketDtos));
        }
    }
}
