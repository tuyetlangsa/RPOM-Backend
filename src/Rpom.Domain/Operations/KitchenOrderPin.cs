using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Sales;

namespace Rpom.Domain.Operations;

/// <summary>
///     Ghim 1 đợt (Order) lên đầu màn KDS của MỘT khu bếp. Scope theo (Station, Order) — mỗi bếp tự
///     ghim độc lập. Order ghim nổi lên đầu danh sách order TRONG area của nó; ghim sau (PinnedAt
///     lớn hơn) nằm trên ghim trước. Có dòng = đang ghim; unpin = xoá dòng.
/// </summary>
public class KitchenOrderPin : Entity
{
    public int KitchenStationId { get; set; }
    public long OrderId { get; set; }

    /// <summary>Mốc ghim — dùng để xếp ghim-sau lên trên ghim-trước (PinnedAt desc).</summary>
    public DateTime PinnedAt { get; set; }

    /// <summary>Nhân viên bếp ghim. Soft ref.</summary>
    public int PinnedByStaffId { get; set; }

    public virtual KitchenStation KitchenStation { get; set; } = null!;
    public virtual Order Order { get; set; } = null!;
}
