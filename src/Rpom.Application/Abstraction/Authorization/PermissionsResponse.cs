namespace Rpom.Application.Abstraction.Authorization;

public sealed record PermissionsResponse(int StaffAccountId, HashSet<string> Permissions);
