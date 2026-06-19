using Rpom.Domain.Common;
using Rpom.Domain.Menu;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;
public class ItemAreaLock : Entity
{
    public int ItemId { get; set; }
    public int AreaId { get; set; }
    public int LockedByStaffId { get; set; }
    public string? Note { get; set; }
    public DateTime LockedAt { get; set; }

    public virtual Item Item { get; set; } = null!;
    public virtual Area Area { get; set; } = null!;
}
