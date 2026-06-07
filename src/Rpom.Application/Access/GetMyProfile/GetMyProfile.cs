using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Application.Abstraction.User;
using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetMyProfile;

public static class GetMyProfile
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(
        int StaffAccountId,
        string Username,
        string FullName,
        string? Phone,
        string? Email,
        string RoleCode,
        string RoleName,
        IReadOnlyList<string> Permissions);

    internal sealed class Handler(IDbContext dbContext, ICurrentStaff currentStaff)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            int staffId = currentStaff.StaffAccountId;

            StaffAccount? staff = await dbContext.StaffAccounts
                .Include(x => x.Role)
                .Include(x => x.StaffAccountPermissions).ThenInclude(x => x.Permission)
                .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);

            if (staff is null)
            {
                return Result.Failure<Response>(AccessErrors.StaffNotFound);
            }

            var permissions = staff.StaffAccountPermissions
                .Select(x => x.Permission.Code)
                .OrderBy(c => c)
                .ToList();

            return Result.Success(new Response(
                staff.Id,
                staff.Username,
                staff.FullName,
                staff.Phone,
                staff.Email,
                staff.Role.Code,
                staff.Role.Name,
                permissions));
        }
    }
}
