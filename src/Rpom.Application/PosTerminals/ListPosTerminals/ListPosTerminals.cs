using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.PosTerminals.ListPosTerminals;
public static class ListPosTerminals
{
    public sealed record Query(int? CounterId) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Terminal> Terminals);

    public sealed record Terminal(
        int Id,
        int CounterId,
        string CounterName,
        string Name,
        bool HasDisplay,
        DateTime? LastSeenAt,
        bool IsActive);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var rows = await db.PosTerminals
                .Where(t => request.CounterId == null || t.CounterId == request.CounterId)
                .OrderBy(t => t.CounterId).ThenBy(t => t.Name)
                .Select(t => new Terminal(
                    t.Id, t.CounterId, t.Counter.Name, t.Name,
                    db.CustomerDisplays.Any(d => d.PosTerminalId == t.Id && d.IsActive),
                    t.LastSeenAt, t.IsActive))
                .ToListAsync(ct);

            return Result.Success(new Response(rows));
        }
    }
}
