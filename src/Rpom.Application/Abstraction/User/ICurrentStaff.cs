namespace Rpom.Application.Abstraction.User;

/// <summary>
///     Per-request accessor for the currently authenticated staff account.
///     Resolves from HttpContext.User claims (set by JWT auth + CustomClaimsTransformation).
/// </summary>
public interface ICurrentStaff
{
    /// <summary>StaffAccount.Id of the caller.</summary>
    int StaffAccountId { get; }

    /// <summary>Username snapshot from JWT claim (avoid extra DB query for display).</summary>
    string Username { get; }

    /// <summary>Effective permission codes for the current request.</summary>
    HashSet<string> GetPermissions();
}
