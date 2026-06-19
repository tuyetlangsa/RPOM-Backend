using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;

/// <summary>
///     Per-staff read cursor cho thông báo broadcast của một quầy. Vì <see cref="StaffNotification"/>
///     broadcast theo Counter (nhiều người cùng quầy) nên "đã xem" là per-staff: lưu
///     <see cref="LastReadNotificationId"/> = notification mới nhất nhân viên đã xem tại quầy đó.
///     Badge chưa đọc = số <see cref="StaffNotification"/> của quầy có <c>Id &gt; LastReadNotificationId</c>.
///     Key theo (Staff, Counter) để xử lý đúng khi nhân viên đổi quầy giữa ca.
/// </summary>
public class NotificationReadState : Entity
{
    public int StaffAccountId { get; set; }
    public int CounterId { get; set; }

    /// <summary>Notification Id lớn nhất đã xem. Soft ref (no FK) — cursor value.</summary>
    public long LastReadNotificationId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual StaffAccount StaffAccount { get; set; } = null!;
    public virtual Counter Counter { get; set; } = null!;
}
