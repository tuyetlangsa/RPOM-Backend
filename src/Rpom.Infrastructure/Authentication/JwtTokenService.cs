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

    public AccessTokenResult IssueAccessToken(int staffAccountId, string username, int? kitchenStationId = null)
    {
        var claims = new List<Claim>
        {
            new(CustomClaims.Sub, staffAccountId.ToString()),
            new(CustomClaims.Username, username)
        };

        if (kitchenStationId.HasValue)
            claims.Add(new Claim(CustomClaims.KitchenStationId, kitchenStationId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        DateTime now = dateTimeProvider.UtcNow;
        DateTime expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now,
            expiresAt,
            creds);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
