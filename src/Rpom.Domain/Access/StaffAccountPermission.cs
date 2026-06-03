using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
/// THE permission assignment table. Direct M:N: account ↔ permission.
/// Composite PK (StaffAccountId, PermissionId). Runtime auth check is a single
/// membership test. No "IsGranted" override — assignment exists or it doesn't.
/// </summary>
public class StaffAccountPermission : Entity
{
    public int StaffAccountId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual StaffAccount StaffAccount { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
