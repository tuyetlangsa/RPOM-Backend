using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Common;
using Rpom.Domain.Sales.CashDrawer;

namespace Rpom.Application.CashDrawers.GetCurrentCashDrawer;

/// <summary>
///     Returns the OPEN drawer at a counter (if any). Used by the cashier app
///     on startup to decide whether to prompt the operator to open a drawer.
/// </summary>
public static class GetCurrentCashDrawer
{
    public sealed record Query(int CounterId) : IQuery<Response?>;

    public sealed record Response(
        long Id,
        int CounterId,
        int OpenedByStaffAccountId,
        string OpenedByStaffName,
        DateTime OpenedAt,
        decimal OpeningCash,
        string Status,
        string? Notes);

    internal sealed class Handler(IDbContext dbContext) : IQueryHandler<Query, Response?>
    {
        public async Task<Result<Response?>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await dbContext.CashDrawerSessions
                .Where(x => x.CounterId == request.CounterId && x.Status == CashDrawerStatus.Open)
                .Select(x => new Response(
                    x.Id, x.CounterId, x.OpenedByStaffAccountId,
                    x.OpenedByStaff.FullName,
                    x.OpenedAt, x.OpeningCash, x.Status, x.Notes))
                .FirstOrDefaultAsync(ct);

            return Result.Success(row);
        }
    }
}
