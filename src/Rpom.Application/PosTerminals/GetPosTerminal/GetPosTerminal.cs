using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.PosTerminals.GetPosTerminal;
public static class GetPosTerminal
{
    public sealed record Query(int Id) : IQuery<Response>;

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

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await db.PosTerminals
                .Where(t => t.Id == request.Id)
                .Select(t => new Response(
                    t.Id, t.CounterId, t.Counter.Name, t.Name,
                    db.CustomerDisplays.Any(d => d.PosTerminalId == t.Id && d.IsActive),
                    t.LastSeenAt, t.IsActive, t.CreatedAt, t.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null ? Result.Failure<Response>(PosTerminalErrors.NotFound) : Result.Success(row);
        }
    }
}
