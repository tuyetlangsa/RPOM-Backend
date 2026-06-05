using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rpom.Application.Abstraction.Authentication;
using Rpom.Application.Abstraction.Clock;

namespace Rpom.Infrastructure.Authentication;

internal sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IDateTimeProvider dateTimeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult IssueAccessToken(int staffAccountId, string username)
    {
        var claims = new List<Claim>
        {
            new(CustomClaims.Sub, staffAccountId.ToString()),
            new(CustomClaims.Username, username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: creds);

        return new AccessTokenResult(
            Token: new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt: expiresAt);
    }
}
