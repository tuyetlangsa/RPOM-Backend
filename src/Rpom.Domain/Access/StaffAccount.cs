using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     Restaurant staff user account. Identity for all auth + audit. Hub entity
///     referenced by 8 subject areas. Effective permissions = SELECT FROM
///     StaffAccountPermission WHERE StaffAccountId = @id (flat, no inheritance).
///     Actor info for audit captured in AuditLog with EntityType='StaffAccount'.
/// </summary>
public class StaffAccount : Entity
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;

    /// <summary>BCrypt hash; plain password never stored.</summary>
    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>1 account = exactly 1 role (label only — does not determine permissions).</summary>
    public int RoleId { get; set; }

    /// <summary>Soft delete; account history preserved.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Manual lock by Owner; separate from IsActive.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Updated by login flow; NULL for never-logged accounts.</summary>
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<StaffAccountPermission> StaffAccountPermissions { get; set; } =
        new List<StaffAccountPermission>();
}
