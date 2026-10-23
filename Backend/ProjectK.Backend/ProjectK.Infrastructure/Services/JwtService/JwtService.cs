using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Infrastructure.Services.JwtService;

public class JwtService : IJwtService
{
    private const string PurposeClaim = "purpose";
    private const string MfaChallengePurpose = "mfa-challenge";
    private static readonly TimeSpan MfaChallengeLifetime = TimeSpan.FromMinutes(5);

    // The dev role switcher's way back to the administrator: long enough for a testing session,
    // short enough that a ticket left in a browser is not a standing door.
    private const string DevReturnPurpose = "dev-return";
    private static readonly TimeSpan DevReturnLifetime = TimeSpan.FromHours(12);

    private const string MfaTrustPurpose = "mfa-trust";
    private const string SecurityStampClaim = "stamp";
    private const int DefaultMfaTrustDays = 7;

    private readonly IConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public JwtService(IConfiguration config, TimeProvider timeProvider)
    {
        _config = config;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Генерує JWT access token
    /// </summary>
    public string GenerateAccessToken(string userId, string email, IEnumerable<string> roles, string? kurinKey, bool hasSecondFactor)
    {
        var now = _timeProvider.GetUtcNow();
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(AccessTokenClaims.AuthenticationMethods, hasSecondFactor ? AccessTokenClaims.Mfa : AccessTokenClaims.Password),
            new Claim(AccessTokenClaims.IssuedAt, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (kurinKey != null)
        {
            claims.Add(new Claim(AccessTokenClaims.KurinKey, kurinKey));
        }

        var creds = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: now.UtcDateTime.AddMinutes(int.Parse(_config["Jwt:ExpiresInMinutes"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Генерує refresh token
    /// </summary>
    public RefreshToken GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        return new RefreshToken
        {
            Token = Convert.ToBase64String(randomBytes),
            Expires = _timeProvider.GetUtcNow().UtcDateTime.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiresInDays"])),
            Created = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public string GenerateMfaChallengeToken(Guid userId) => GeneratePurposeToken(userId, MfaChallengePurpose, MfaChallengeLifetime);

    /// <inheritdoc />
    public Guid? ReadMfaChallenge(string token) => ReadPurposeToken(token, MfaChallengePurpose);

    /// <inheritdoc />
    public string GenerateDevReturnTicket(Guid userId) => GeneratePurposeToken(userId, DevReturnPurpose, DevReturnLifetime);

    /// <inheritdoc />
    public Guid? ReadDevReturnTicket(string token) => ReadPurposeToken(token, DevReturnPurpose);

    public MfaTrustGrant GenerateMfaTrustToken(Guid userId, string securityStamp)
    {
        var days = int.TryParse(_config["Security:MfaTrustDays"], out var configured) && configured > 0
            ? configured
            : DefaultMfaTrustDays;
        var lifetime = TimeSpan.FromDays(days);
        var token = GeneratePurposeToken(userId, MfaTrustPurpose, lifetime, [new Claim(SecurityStampClaim, securityStamp)]);
        return new MfaTrustGrant(token, _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime));
    }

    public MfaTrust? ReadMfaTrust(string token)
    {
        var principal = ReadPurposePrincipal(token, MfaTrustPurpose);
        if (principal is null || !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            return null;
        }

        var stamp = principal.FindFirst(SecurityStampClaim)?.Value;
        return stamp is null ? null : new MfaTrust(userId, stamp);
    }

    /// <summary>
    /// A short-lived token that proves one thing about one account and nothing else. Its own
    /// audience, so the bearer middleware never accepts it as an access token: the two are
    /// signed with the same key and would otherwise look alike.
    /// </summary>
    private string GeneratePurposeToken(Guid userId, string purpose, TimeSpan lifetime, IEnumerable<Claim>? extraClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(PurposeClaim, purpose)
        };
        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: PurposeAudience(purpose),
            claims: claims,
            expires: _timeProvider.GetUtcNow().UtcDateTime.Add(lifetime),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Guid? ReadPurposeToken(string token, string purpose)
    {
        var principal = ReadPurposePrincipal(token, purpose);
        return principal is not null && Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            ? userId
            : null;
    }

    private ClaimsPrincipal? ReadPurposePrincipal(string token, string purpose)
    {
        // Claims are read as written: the default handler renames "sub" to NameIdentifier on the
        // way in, which is a surprise nobody needs here.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = PurposeAudience(purpose),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal.FindFirst(PurposeClaim)?.Value == purpose ? principal : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

    private string PurposeAudience(string purpose) => $"{_config["Jwt:Audience"]}:{purpose}";
}
