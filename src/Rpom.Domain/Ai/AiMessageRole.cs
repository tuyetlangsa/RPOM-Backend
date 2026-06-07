namespace Rpom.Domain.Ai;

/// <summary>
///     AiMessage.Role values.
///     USER: user typed message. ASSISTANT: AI response text.
///     TOOL_CALL: record of a tool invocation (JSON in Content, hidden in UI).
///     SYSTEM: system prompt / context injection (hidden in UI).
/// </summary>
public static class AiMessageRole
{
    public const string User = "USER";
    public const string Assistant = "ASSISTANT";
    public const string ToolCall = "TOOL_CALL";
    public const string System = "SYSTEM";
}
