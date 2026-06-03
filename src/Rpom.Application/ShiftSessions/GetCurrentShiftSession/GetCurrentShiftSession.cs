using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Application.ShiftSessions.Shared;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Application.ShiftSessions.GetCurrentShiftSession;

public static class GetCurrentShiftSession
{
    public sealed record Query : IQuery<ShiftSessionSummary>;

    internal sealed class Handler(IDbContext dbContext, ICurrentStaff currentStaff)
        : IQueryHandler<Query, ShiftSessionSummary>
    {
        public async Task<Result<ShiftSessionSummary>> Handle(Query request, CancellationToken ct)
        {
            var summary = await ShiftSessionMapper.LoadCurrentForStaffAsync(
                dbContext, currentStaff.StaffAccountId, ct);

            return summary is null
                ? Result.Failure<ShiftSessionSummary>(ShiftSessionErrors.NotFound)
                : Result.Success(summary);
        }
    }
}
