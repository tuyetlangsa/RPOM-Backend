using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using TicketErrors = Rpom.Domain.Sales.TicketErrors;

namespace Rpom.Application.Tickets.GetTicketAuditLog;

public static class GetTicketAuditLog
{
    public sealed record Query(long TicketId) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        long Id,
        string Action,
        string? ActorFullName,
        DateTime Timestamp,
        string? Summary);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query q, CancellationToken ct)
        {
            bool ticketExists = await db.Tickets.AnyAsync(t => t.Id == q.TicketId, ct);
            if (!ticketExists) return Result.Failure<IReadOnlyList<Response>>(TicketErrors.NotFound);

            var logs = await db.AuditLogs
                .Where(a => a.EntityType == "Ticket" && a.EntityId == q.TicketId)
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new Response(
                    a.Id,
                    a.Action,
                    a.ActorFullName,
                    a.Timestamp,
                    a.Summary))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(logs);
        }
    }
}
