using Rpom.Domain.Common;

namespace Rpom.Domain.Operations;

/// <summary>
///     Màn hình hướng KHÁCH (máy riêng) gắn CỐ ĐỊNH với một <see cref="PosTerminal"/> (1:1) lúc
///     đăng ký. Poll bằng <see cref="DeviceToken"/>; hiển thị QR PENDING của terminal đã gắn, lúc
///     rảnh hiển thị <see cref="IdleMediaUrl"/> (fallback config global). Không pair theo ca nữa —
///     binding vĩnh viễn theo máy POS. Activation: token chỉ được claim bởi 1 máy khách
///     (<see cref="ActivatedClientId"/>) — máy thứ 2 dùng cùng token bị từ chối.
/// </summary>
public class CustomerDisplay : Entity
{
    public int Id { get; set; }

    /// <summary>Máy POS gắn cố định (1:1). QR của terminal này sẽ hiển thị.</summary>
    public int PosTerminalId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Token bí mật của thiết bị màn khách, gửi mỗi lần poll. Provision 1 lần vào máy.</summary>
    public string DeviceToken { get; set; } = null!;

    /// <summary>
    ///     Định danh máy khách đã "claim" token (sinh phía client lần đầu activate). NULL = chưa
    ///     activate. Poll bắt buộc clientId khớp → 1 token chỉ 1 máy dùng được.
    /// </summary>
    public string? ActivatedClientId { get; set; }

    public DateTime? ActivatedAt { get; set; }

    /// <summary>URL ảnh/video lúc rảnh; NULL → dùng config global <c>customer_display.idle_media_url</c>.</summary>
    public string? IdleMediaUrl { get; set; }

    /// <summary>Lần poll gần nhất — heartbeat.</summary>
    public DateTime? LastSeenAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual PosTerminal PosTerminal { get; set; } = null!;
}
