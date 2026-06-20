using Rpom.Domain.Access;
using Rpom.Domain.Common;
using Rpom.Domain.Restaurant;

namespace Rpom.Domain.Operations;

/// <summary>
///     Màn hình hướng KHÁCH (máy riêng, độc lập với máy cashier) đặt tại một quầy. Lúc rảnh hiển
///     thị ảnh/video (<see cref="IdleMediaUrl"/>, fallback config global); khi cashier đã ghép cặp
///     tạo QR payment thì hiện QR cho khách quét. Ghép cặp theo CASHIER (<see cref="PairedStaffAccountId"/>)
///     vì 1 quầy chỉ có 1 drawer dùng chung, mỗi máy cashier = 1 staff đăng nhập. Auth thiết bị qua
///     <see cref="DeviceToken"/> (không phải JWT staff). Cashier ghép bằng cách nhập <see cref="PairingCode"/>.
/// </summary>
public class CustomerDisplay : Entity
{
    public int Id { get; set; }

    /// <summary>Quầy mà màn hình này phục vụ.</summary>
    public int CounterId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Token bí mật của thiết bị, gửi mỗi lần poll để xác thực. KHÔNG phải JWT staff.</summary>
    public string DeviceToken { get; set; } = null!;

    /// <summary>Mã ghép cặp hiển thị trên màn hình khi chưa ghép — cashier nhập để ghép.</summary>
    public string PairingCode { get; set; } = null!;

    /// <summary>Cashier (staff) đang được ghép — QR của staff này sẽ hiển thị. NULL = chưa ghép.</summary>
    public int? PairedStaffAccountId { get; set; }

    public DateTime? PairedAt { get; set; }

    /// <summary>URL ảnh/video lúc rảnh; NULL → dùng config global <c>customer_display.idle_media_url</c>.</summary>
    public string? IdleMediaUrl { get; set; }

    /// <summary>Lần poll gần nhất — dùng cho heartbeat / dọn pairing treo.</summary>
    public DateTime? LastSeenAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Counter Counter { get; set; } = null!;
    public virtual StaffAccount? PairedStaff { get; set; }
}
