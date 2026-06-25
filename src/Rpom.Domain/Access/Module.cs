using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     UI module — top tier of the navigation-access tree (nexterp, cashier, order, kitchen).
///     Catalog only: dev-seeded, not user-editable, does NOT gate API calls (permissions do that).
///     A module is "visible" to an account iff the account has access to ≥1 of its Pages.
/// </summary>
public class Module : Entity
{
    public int Id { get; set; }

    /// <summary>Machine-readable: nexterp, cashier, order, kitchen.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
    public short DisplayOrder { get; set; }

    public virtual ICollection<Page> Pages { get; set; } = new List<Page>();
}
