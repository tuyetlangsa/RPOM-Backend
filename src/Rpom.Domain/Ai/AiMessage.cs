using Rpom.Domain.Common;

namespace Rpom.Domain.Ai;

/// <summary>
///     Single message in an AiConversation. Weak entity — cannot exist without parent.
///     Append-only by service contract. Actor derived from parent OwnerStaffId for USER,
///     "system" for non-USER messages (no CreatedByStaffId column).
/// </summary>
public class AiMessage : Entity
{
    public long Id { get; set; }

    /// <summary>Parent conversation. Cascade delete with conversation.</summary>
    public long ConversationId { get; set; }

    /// <summary>USER | ASSISTANT | TOOL_CALL | SYSTEM (see <see cref="AiMessageRole" />).</summary>
    public string Role { get; set; } = null!;

    /// <summary>Message body. For USER/ASSISTANT/SYSTEM: text. For TOOL_CALL: JSON of tool input/output.</summary>
    public string Content { get; set; } = null!;

    /// <summary>Order within conversation (1, 2, 3, ...). Stable; no insertions between.</summary>
    public int SequenceNumber { get; set; }

    /// <summary>LLM token count snapshot — for cost tracking + context-window management.</summary>
    public int? TokenCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AiConversation Conversation { get; set; } = null!;
    public virtual ICollection<AiToolCallLog> ToolCalls { get; set; } = new List<AiToolCallLog>();
}
