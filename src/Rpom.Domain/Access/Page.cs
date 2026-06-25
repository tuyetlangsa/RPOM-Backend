using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     UI page — second tier of the navigation-access tree, belongs to one Module.
///     The atomic unit granted to an account via StaffAccountPageAccess (mirrors Permission).
///     FE owns the pageCode → route mapping; BE stores only Code.
/// </summary>
public class Page : Entity
{
    public int Id { get; set; }

    /// <summary>Module-prefixed, globally unique: cashier.tickets, cashier.floor_plan.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public int ModuleId { get; set; }
    public short DisplayOrder { get; set; }

    public virtual Module Module { get; set; } = null!;

    public virtual ICollection<StaffAccountPageAccess> StaffAccountPageAccesses { get; set; } =
        new List<StaffAccountPageAccess>();
}
