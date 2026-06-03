namespace Rpom.Domain.Ai;

/// <summary>AiNotification.Type values.</summary>
public static class AiNotificationType
{
    /// <summary>Low-stock alert from 30-min cron over ItemStock.</summary>
    public const string LowStock = "LOW_STOCK";

    /// <summary>End-of-day summary from 18:00 daily cron.</summary>
    public const string EodSummary = "EOD_SUMMARY";
}
