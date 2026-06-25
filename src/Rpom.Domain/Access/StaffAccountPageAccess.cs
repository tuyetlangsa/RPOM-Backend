using Rpom.Domain.Common;

namespace Rpom.Domain.Access;

/// <summary>
///     THE page-access assignment table. Direct M:N: account ↔ page.
///     Composite PK (StaffAccountId, PageId). Navigation gate is a single membership test.
///     No "IsGranted" override — assignment exists or it doesn't. Append-only contract.
/// </summary>
public class StaffAccountPageAccess : Entity
{
    public int StaffAccountId { get; set; }
    public int PageId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual StaffAccount StaffAccount { get; set; } = null!;
    public virtual Page Page { get; set; } = null!;
}
