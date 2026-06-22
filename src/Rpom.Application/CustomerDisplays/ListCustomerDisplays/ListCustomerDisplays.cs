using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;

namespace Rpom.Application.CustomerDisplays.ListCustomerDisplays;
public static class ListCustomerDisplays
{
    public sealed record Query(int? CounterId) : IQuery<Response>;

    public sealed record Response(IReadOnlyList<Display> Displays);

    public sealed record Display(
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
        bool IsActive);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            var rows = await db.CustomerDisplays
                .Where(d => request.CounterId == null || d.PosTerminal.CounterId == request.CounterId)
                .OrderBy(d => d.PosTerminal.CounterId).ThenBy(d => d.Name)
                .Select(d => new Display(
                    d.Id, d.PosTerminalId, d.PosTerminal.Name,
                    d.PosTerminal.CounterId, d.PosTerminal.Counter.Name, d.Name,
                    d.ActivatedClientId != null, d.ActivatedAt,
                    d.IdleMediaUrl, d.LastSeenAt, d.IsActive))
                .ToListAsync(ct);

            return Result.Success(new Response(rows));
        }
    }
}
