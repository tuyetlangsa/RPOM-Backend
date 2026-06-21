using Microsoft.EntityFrameworkCore;
using Rpom.Application.Abstraction.Data;
using Rpom.Application.Abstraction.Messaging;
using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Application.Access.GetStaffAccount;

public static class GetStaffAccount
{
    public sealed record Query(int Id) : IQuery<Response>;

    public sealed record Response(
        int Id,
        string Username,
        string FullName,
        string? Phone,
        string? Email,
        int RoleId,
        string RoleCode,
        string RoleName,
        bool IsActive,
        bool IsLocked,
        DateTime? LastLoginAt,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    internal sealed class Handler(IDbContext db) : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            Response? row = await db.StaffAccounts
                .Where(x => x.Id == request.Id)
                .Select(x => new Response(
                    x.Id, x.Username, x.FullName, x.Phone, x.Email,
                    x.RoleId, x.Role.Code, x.Role.Name,
                    x.IsActive, x.IsLocked, x.LastLoginAt, x.CreatedAt, x.UpdatedAt))
                .FirstOrDefaultAsync(ct);

            return row is null
                ? Result.Failure<Response>(AccessErrors.StaffNotFound)
                : Result.Success(row);
        }
    }
}
