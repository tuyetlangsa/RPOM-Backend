using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.CustomerDisplays.GetCustomerDisplay;
public static class GetCustomerDisplay
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        int PosTerminalId,
        string PosTerminalName,
        int CounterId,
        string CounterName,
        string Name,
        bool IsActivated,
        DateTime? ActivatedAt,
        string? IdleMediaUrl,
        DateTime? LastSeenAt,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await db.CustomerDisplays
                .Where(d => d.Id == request.Id)
                .Select(d => new Response(
                    d.Id, d.PosTerminalId, d.PosTerminal.Name,
                    d.PosTerminal.CounterId, d.PosTerminal.Counter.Name, d.Name,
                    d.ActivatedClientId != null, d.ActivatedAt,
                    d.IdleMediaUrl, d.LastSeenAt, d.IsActive, d.CreatedAt, d.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null ? Result.Failure<Response>(CustomerDisplayErrors.NotFound) : Result.Success(row);
        }
    }
}
