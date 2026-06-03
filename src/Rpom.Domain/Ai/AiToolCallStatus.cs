namespace Rpom.Domain.Ai;

/// <summary>AiToolCallLog.Status values set by the tool layer.</summary>
public static class AiToolCallStatus
{
    public const string Success = "SUCCESS";
    public const string Error = "ERROR";
    public const string Timeout = "TIMEOUT";

    /// <summary>User lacks the permission this tool requires (RBAC check failed).</summary>
    public const string RejectedPermission = "REJECTED_PERMISSION";
}
