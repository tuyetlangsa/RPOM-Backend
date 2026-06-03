namespace Rpom.Domain.Ai;

/// <summary>
/// AiNotification.Status values.
/// UNREAD → READ (user opens) → DISMISSED (user clicks dismiss).
/// Any → ARCHIVED (cron archives after 30 days; UI hides from inbox).
/// </summary>
public static class AiNotificationStatus
{
    public const string Unread = "UNREAD";
    public const string Read = "READ";
    public const string Dismissed = "DISMISSED";
    public const string Archived = "ARCHIVED";
}
