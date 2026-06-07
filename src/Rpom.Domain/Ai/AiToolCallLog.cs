using Rpom.Domain.Common;

namespace Rpom.Domain.Ai;

/// <summary>
///     Audit trace of tool invocations within an ASSISTANT message. Sub-detail of AiMessage.
///     Indexable for metrics (usage, latency) + permission audit (REJECTED_PERMISSION).
///     Every tool enforces calling user's permissions via StaffAccountPermission.
/// </summary>
public class AiToolCallLog : Entity
{
    public long Id { get; set; }

    /// <summary>Parent ASSISTANT message that issued this tool call.</summary>
    public long MessageId { get; set; }

    /// <summary>getSalesByPeriod | getStockLevel | predictStockoutTime | ... (10 v1 tools per Spec §5).</summary>
    public string ToolName { get; set; } = null!;

    /// <summary>Tool input parameters as JSON.</summary>
    public string InputJson { get; set; } = null!;

    /// <summary>Tool output as JSON. NULL if tool errored.</summary>
    public string? OutputJson { get; set; }

    /// <summary>SUCCESS | ERROR | TIMEOUT | REJECTED_PERMISSION (see <see cref="AiToolCallStatus" />).</summary>
    public string Status { get; set; } = AiToolCallStatus.Success;

    /// <summary>Populated when Status != SUCCESS.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Execution time in ms — for performance monitoring + §10 evaluation metrics.</summary>
    public int? LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AiMessage Message { get; set; } = null!;
}
