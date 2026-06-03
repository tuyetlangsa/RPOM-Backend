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
            var staffId = currentStaff.StaffAccountId;

            var staff = await dbContext.StaffAccounts
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
                StaffAccountId: staff.Id,
                Username: staff.Username,
                FullName: staff.FullName,
                Phone: staff.Phone,
                Email: staff.Email,
                RoleCode: staff.Role.Code,
                RoleName: staff.Role.Name,
                Permissions: permissions));
        }
    }
}
