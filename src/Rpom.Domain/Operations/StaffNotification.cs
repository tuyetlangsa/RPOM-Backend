using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;

/// <summary>
///     Broadcast operational notification to staff terminals (Cashier / Order Staff),
///     scoped by Counter — NOT per-recipient. FE polls by the staff's current counter
///     (e.g. <c>WHERE CounterId = X AND CreatedAt &gt; since</c>) and renders a banner.
///     v1 use: item out-of-stock / back-in-stock (F5). RefItemId is a soft reference (no FK).
/// </summary>
public class StaffNotification : Entity
{
    public long Id { get; set; }
    /// <summary>Broadcast scope — every terminal on this counter sees it.</summary>
    public int CounterId { get; set; }

    /// <summary>
    ///     Area cụ thể mà cảnh báo áp dụng (vd món hết hàng ở area nào). NULL nếu không gắn area.
    ///     Một counter có thể nhiều area → giúp FOH biết chính xác area thay vì cả quầy.
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>ITEM_OUT_OF_STOCK | ITEM_BACK_IN_STOCK (see <see cref="StaffNotificationType" />).</summary>
    public string Type { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    /// <summary>Soft ref to the Item this alert is about (NO FK). NULL if not item-specific.</summary>
    public int? RefItemId { get; set; }

    /// <summary>Staff who triggered it (kitchen staff). Soft ref.</summary>
    public int CreatedByStaffId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual Area? Area { get; set; }
}
