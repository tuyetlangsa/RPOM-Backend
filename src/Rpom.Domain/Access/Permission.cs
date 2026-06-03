using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
/// Smallest unit of access control — 1:1 with a user-facing action/button.
/// Runtime auth check tests membership in StaffAccountPermission (flat assignment).
/// Belongs to exactly one PermissionGroup (UI grouping only). Seeded by developers.
/// </summary>
public class Permission : Entity
{
    public int Id { get; set; }

    /// <summary>Machine-readable: view_revenue_report, reopen_ticket, ask_ai, ...</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int PermissionGroupId { get; set; }
    public short DisplayOrder { get; set; }

    public virtual PermissionGroup PermissionGroup { get; set; } = null!;
    public virtual ICollection<StaffAccountPermission> StaffAccountPermissions { get; set; } = new List<StaffAccountPermission>();
}
