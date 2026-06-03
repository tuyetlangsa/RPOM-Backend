using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Domain.Ai;

/// <summary>
/// Proactive cron-driven notification from AI Operations Assistant.
/// Separate from AiConversation — push notifications are not chat messages.
/// RefItemId / RefCounterId are polymorphic (NO FK) — may target later
/// soft-deleted rows; notification stays readable in archive.
/// </summary>
public class AiNotification : Entity
{
    public long Id { get; set; }

    /// <summary>Manager/Owner receiving the notification.</summary>
    public int RecipientStaffId { get; set; }

    /// <summary>LOW_STOCK | EOD_SUMMARY (see <see cref="AiNotificationType"/>); future ANOMALY_ALERT, FORECAST.</summary>
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;

    /// <summary>Full AI-generated narrative + recommended action (markdown allowed).</summary>
    public string Body { get; set; } = null!;

    /// <summary>For LOW_STOCK: the at-risk Item. NULL otherwise. NO FK (polymorphic).</summary>
    public int? RefItemId { get; set; }

    /// <summary>For EOD_SUMMARY scoped to specific Counter. NULL = all counters. NO FK.</summary>
    public int? RefCounterId { get; set; }

    /// <summary>UNREAD | READ | DISMISSED | ARCHIVED (see <see cref="AiNotificationStatus"/>).</summary>
    public string Status { get; set; } = AiNotificationStatus.Unread;
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }

    /// <summary>When the cron that produced this notification fired.</summary>
    public DateTime TriggeredByCronAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — notification inbox refresh + badge count.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual StaffAccount RecipientStaff { get; set; } = null!;
}
