using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.PosTerminals.ListPosTerminals;
public static class ListPosTerminals
{
    public sealed record Query(int? CounterId, string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

    public sealed record Response(
        int Id,
        int CounterId,
        string CounterName,
        string Name,
        bool HasDisplay,
        DateTime? LastSeenAt,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<PosTerminal> q = db.PosTerminals.AsQueryable();

            if (request.CounterId.HasValue)
                q = q.Where(t => t.CounterId == request.CounterId.Value);
            if (request.IsActive.HasValue)
                q = q.Where(t => t.IsActive == request.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(t => t.Name.ToLower().Contains(s));
            }

            List<Response> rows = await q
                .OrderBy(t => t.CounterId).ThenBy(t => t.Name)
                .Select(t => new Response(
                    t.Id, t.CounterId, t.Counter.Name, t.Name,
                    db.CustomerDisplays.Any(d => d.PosTerminalId == t.Id && d.IsActive),
                    t.LastSeenAt, t.IsActive, t.CreatedAt, t.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
