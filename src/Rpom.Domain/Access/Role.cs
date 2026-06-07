using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     LABEL ONLY — categorizes the type of staff for display/filtering/reports.
///     Does NOT directly own permissions (those live in StaffAccountPermission).
///     System roles seeded: Owner, Manager, Cashier, Order Staff, Kitchen Staff, Admin Vendor.
/// </summary>
public class Role : Entity
{
    public int Id { get; set; }

    /// <summary>OWNER, MANAGER, CASHIER, ORDER_STAFF, KITCHEN_STAFF, ADMIN_VENDOR, or custom.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>System roles cannot be deleted, only deactivated.</summary>
    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<StaffAccount> StaffAccounts { get; set; } = new List<StaffAccount>();
}
