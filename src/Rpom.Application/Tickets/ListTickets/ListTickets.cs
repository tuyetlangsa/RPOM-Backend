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
                .Select(t => new
                {
                    t.Id, t.Code, t.TableId, TableCode = t.Table.Code,
                    t.AreaId, AreaName = t.Area.Name,
                    t.CounterId, CounterName = t.Counter.Name,
                    t.Status, t.GuestCount,
                    WaiterName = t.WaiterStaff != null ? t.WaiterStaff.FullName : null,
                    t.OpenedAt, t.ClosedAt, t.TotalAmount, t.PaidAmount
                })
                .ToListAsync(ct);

            // Load payment methods separately
            var ticketIds = tickets.Select(t => t.Id).ToList();
            var payByTicket = ticketIds.Count > 0
                ? (await db.TicketPaymentDetails
                    .Where(p => ticketIds.Contains(p.TicketId) && p.Status == Domain.Sales.TicketPaymentStatus.Success)
                    .Select(p => new { p.TicketId, p.PaymentMethod.Code })
                    .Distinct()
                    .ToListAsync(ct))
                .GroupBy(p => p.TicketId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Code).Distinct().OrderBy(c => c).ToList())
                : new Dictionary<long, List<string>>();

            var items = tickets.Select(t => new Response(
                t.Id, t.Code, t.TableId, t.TableCode, t.AreaId, t.AreaName,
                t.CounterId, t.CounterName, t.Status, t.GuestCount, t.WaiterName,
                t.OpenedAt, t.ClosedAt, t.TotalAmount, t.PaidAmount,
                t.TotalAmount - t.PaidAmount,
                string.Join(" + ", payByTicket.GetValueOrDefault(t.Id, [])),
                0)).ToList(); // OrderItemCount = 0 simplified for performance

            return Result.Success(new Page<Response>(items, totalCount, q.PageNumber, q.PageSize));
        }
    }
}
