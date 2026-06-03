using Rpom.Domain.Access;
using Rpom.Domain.Common;

namespace Rpom.Domain.Ai;

/// <summary>
/// Chat session between Manager/Owner and the AI Operations Assistant.
/// Privacy: each conversation is private to OwnerStaffId (no sharing v1).
/// UpdatedAt bumps on every new AiMessage for inbox sort-by-recent.
/// </summary>
public class AiConversation : Entity
{
    public long Id { get; set; }

    /// <summary>Manager or Owner who initiated the chat. Conversation is private to this user.</summary>
    public int OwnerStaffId { get; set; }

    /// <summary>Auto-generated from first USER message; or user-rename.</summary>
    public string? Title { get; set; }

    /// <summary>ACTIVE | ENDED (see <see cref="AiConversationStatus"/>).</summary>
    public string Status { get; set; } = AiConversationStatus.Active;
    public DateTime StartedAt { get; set; }

    /// <summary>NULL while ACTIVE.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Denormalized count of AiMessage rows — for inbox preview "5 messages".</summary>
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Poll cursor — user inbox refresh; bumped on each new AiMessage.</summary>
    public DateTime UpdatedAt { get; set; }

    public virtual StaffAccount OwnerStaff { get; set; } = null!;
    public virtual ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}
