namespace Rpom.Domain.Ai;

/// <summary>AiConversation.Status values. ACTIVE → ENDED (user close or 30-min idle).</summary>
public static class AiConversationStatus
{
    public const string Active = "ACTIVE";
    public const string Ended = "ENDED";
}
