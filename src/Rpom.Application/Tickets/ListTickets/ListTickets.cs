using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.Tickets.ListTickets;

public static class ListTickets
{
    public sealed record Query(
        string? Status,
        DateTime? FromDate,
        DateTime? ToDate,
        int? CounterId,
        int? AreaId,
        int? ShiftId,
        string? Search,
        int PageNumber = 1,
        int PageSize = 50) : IQuery<Page<Response>>;

    public sealed record Response(
        long TicketId,
        string TicketCode,
        int TableId,
        string TableCode,
        int AreaId,
        string AreaName,
        int CounterId,
        string CounterName,
        string Status,
        short GuestCount,
        string? WaiterName,
        DateTime OpenedAt,
        DateTime? ClosedAt,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        string PaymentMethods,
        int OrderItemCount);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Page<Response>>
    {
        public async Task<Result<Page<Response>>> Handle(Query q, CancellationToken ct)
        {
            IQueryable<Domain.Sales.Ticket> query = db.Tickets
                .Include(t => t.Table)
                .Include(t => t.Area)
                .Include(t => t.Counter)
                .Include(t => t.WaiterStaff);

            if (!string.IsNullOrWhiteSpace(q.Status))
                query = query.Where(t => t.Status == q.Status);

            if (q.FromDate.HasValue)
                query = query.Where(t => t.OpenedAt >= q.FromDate.Value);
            if (q.ToDate.HasValue)
                query = query.Where(t => t.OpenedAt <= q.ToDate.Value);

            if (q.CounterId.HasValue)
                query = query.Where(t => t.CounterId == q.CounterId.Value);
            if (q.AreaId.HasValue)
                query = query.Where(t => t.AreaId == q.AreaId.Value);
            if (q.ShiftId.HasValue)
                query = query.Where(t => t.ShiftId == q.ShiftId.Value);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                string s = q.Search.Trim().ToLower();
                query = query.Where(t =>
                    t.Code.ToLower().Contains(s) ||
#pragma warning disable CA1862
                    t.Table.Code.ToLower().Contains(s));
#pragma warning restore CA1862
            }

            int totalCount = await query.CountAsync(ct);

            var tickets = await query
                .OrderByDescending(t => t.OpenedAt)
                .Skip((q.PageNumber - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(t => new Response(
                    t.Id,
                    t.Code,
                    t.TableId,
                    t.Table.Code,
                    t.AreaId,
                    t.Area.Name,
                    t.CounterId,
                    t.Counter.Name,
                    t.Status,
                    t.GuestCount,
                    t.WaiterStaff != null ? t.WaiterStaff.FullName : null,
                    t.OpenedAt,
                    t.ClosedAt,
                    t.TotalAmount,
                    t.PaidAmount,
                    t.TotalAmount - t.PaidAmount,
                    t.Payments
                        .Where(p => p.Status == Domain.Sales.TicketPaymentStatus.Success)
                        .Select(p => p.PaymentMethod.Code)
                        .Distinct()
                        .OrderBy(m => m)
                        .Aggregate("", (acc, m) => acc.Length > 0 ? acc + " + " + m : m),
                    t.Orders
                        .Where(o => o.Status != Domain.Sales.OrderStatus.Draft && o.Status != Domain.Sales.OrderStatus.Deleted)
                        .SelectMany(o => o.OrderItems)
                        .Count(oi => oi.Status != Domain.Sales.OrderItemStatus.Cancelled)))
                .ToListAsync(ct);

            return Result.Success(new Page<Response>(tickets, totalCount, q.PageNumber, q.PageSize));
        }
    }
}
