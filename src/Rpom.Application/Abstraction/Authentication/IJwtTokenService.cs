namespace Rpom.Application.Abstraction.Authentication;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

/// <summary>
/// Optional per-shift scope claims included in the JWT after a staff opens a
/// ShiftSession. Cleared on close (re-issue without scope).
/// </summary>
public sealed record ShiftScopeClaims(
    long ShiftSessionId,
    int? CounterId,
    int? KitchenStationId);

/// <summary>
/// Issues JWT access tokens. Token chỉ chứa minimal claims (sub = StaffAccountId,
/// username); permissions được fetch per-request bởi CustomClaimsTransformation,
/// không bake-in. Sau khi open ShiftSession, token được re-issue với ShiftScopeClaims.
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult IssueAccessToken(int staffAccountId, string username, ShiftScopeClaims? scope = null);
}
