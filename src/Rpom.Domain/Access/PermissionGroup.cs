using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     UI grouping of related permissions, organized by subsystem (common, pos, kds, ...).
///     Purpose is UI categorization only — does NOT participate in runtime auth check.
///     Seeded by developers; not user-editable.
/// </summary>
public class PermissionGroup : Entity
{
    public int Id { get; set; }

    /// <summary>Machine-readable: common, master_data, pos, kds, cashier, reporting.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public short DisplayOrder { get; set; }

    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
