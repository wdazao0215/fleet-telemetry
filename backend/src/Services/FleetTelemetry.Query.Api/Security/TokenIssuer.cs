using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FleetTelemetry.Query.Api.Security;

/// <summary>Emite el JWT del dashboard.</summary>
internal sealed class TokenIssuer(IOptions<JwtOptions> options)
{
    private readonly JwtOptions settings = options.Value;

    public bool AreCredentialsValid(string? username, string? password) =>
        string.Equals(username, settings.OperatorUsername, StringComparison.Ordinal) &&
        string.Equals(password, settings.OperatorPassword, StringComparison.Ordinal);

    public (string Token, DateTimeOffset ExpiresAt) Issue(string username)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(settings.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
