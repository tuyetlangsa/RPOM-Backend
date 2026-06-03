using Microsoft.AspNetCore.Http;
using Rpom.Application.Abstraction.User;
using Rpom.Infrastructure.Authentication;

namespace Rpom.Infrastructure.User;

internal sealed class CurrentStaff(IHttpContextAccessor httpContextAccessor) : ICurrentStaff
{
    public int StaffAccountId =>
        httpContextAccessor.HttpContext?.User.GetStaffAccountId()
        ?? throw new ApplicationException("Staff context is unavailable");

    public string Username =>
        httpContextAccessor.HttpContext?.User.GetUsername()
        ?? throw new ApplicationException("Staff context is unavailable");

    public HashSet<string> GetPermissions() =>
        httpContextAccessor.HttpContext?.User.GetPermissions()
        ?? throw new ApplicationException("Staff context is unavailable");
}
