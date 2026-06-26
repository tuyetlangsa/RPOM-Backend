using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Operations;

namespace Rpom.Application.CustomerDisplays.ListCustomerDisplays;
public static class ListCustomerDisplays
{
    public sealed record Query(int? CounterId, string? Search, bool? IsActive) : IQuery<IReadOnlyList<Response>>;

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

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<Result<IReadOnlyList<Response>>> Handle(Query request, CancellationToken ct)
        {
            IQueryable<CustomerDisplay> q = db.CustomerDisplays.AsQueryable();

            if (request.CounterId.HasValue)
                q = q.Where(d => d.PosTerminal.CounterId == request.CounterId.Value);
            if (request.IsActive.HasValue)
                q = q.Where(d => d.IsActive == request.IsActive.Value);
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                string s = request.Search.Trim().ToLower();
                q = q.Where(d => d.Name.ToLower().Contains(s));
            }

            List<Response> rows = await q
                .OrderBy(d => d.PosTerminal.CounterId).ThenBy(d => d.Name)
                .Select(d => new Response(
                    d.Id, d.PosTerminalId, d.PosTerminal.Name,
                    d.PosTerminal.CounterId, d.PosTerminal.Counter.Name, d.Name,
                    d.ActivatedClientId != null, d.ActivatedAt,
                    d.IdleMediaUrl, d.LastSeenAt, d.IsActive, d.CreatedAt, d.UpdatedAt))
                .ToListAsync(ct);

            return Result.Success<IReadOnlyList<Response>>(rows);
        }
    }
}
