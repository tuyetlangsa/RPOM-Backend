namespace Rpom.Application.Abstraction.Authentication;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

/// <summary>
///     Issues JWT access tokens. Token contains minimal claims (sub = StaffAccountId,
///     username); permissions are fetched per-request by CustomClaimsTransformation
///     rather than baked into the token.
/// </summary>
public interface IJwtTokenService
{
    /// <param name="kitchenStationId">
    ///     Optional kitchen station baked into the token for KDS sessions. NULL for normal sessions.
    /// </param>
    AccessTokenResult IssueAccessToken(int staffAccountId, string username, int? kitchenStationId = null);
}
