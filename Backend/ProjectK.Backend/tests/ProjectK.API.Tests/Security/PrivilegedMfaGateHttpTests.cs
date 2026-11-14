using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ProjectK.API.Middleware;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Infrastructure.Services.Jwt;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The gate end to end: a token minted by <see cref="JwtService"/>, read back through the real
/// bearer handler with its inbound claim mapping, judged by the middleware. The unit tests build
/// the principal by hand and would not notice the handler renaming or dropping <c>amr</c>.
/// </summary>
public class PrivilegedMfaGateHttpTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256";
    private const string Issuer = "lileyka-tests";
    private const string Audience = "lileyka";

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.Forbidden)]
    public async Task APrivilegedMutation_PassesOnlyWhenTheTokenSaysMfa(bool hasSecondFactor, HttpStatusCode expected)
    {
        await using var host = await GateHost.StartAsync();
        var token = Jwt().GenerateAccessToken(Guid.NewGuid().ToString(), "kv@example.com", ["Admin"], null, hasSecondFactor);

        var response = await host.PostAsync(token);

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>A token from before the claim existed says nothing, and nothing is not a second factor.</summary>
    [Fact]
    public async Task ATokenWithoutTheClaim_IsRefused()
    {
        await using var host = await GateHost.StartAsync();
        var legacy = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Admin")]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256)
        });

        var response = await host.PostAsync(legacy);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnOrdinaryMember_IsNotGated()
    {
        await using var host = await GateHost.StartAsync();
        var token = Jwt().GenerateAccessToken(Guid.NewGuid().ToString(), "m@example.com", ["Member"], null, hasSecondFactor: false);

        var response = await host.PostAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static JwtService Jwt() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:ExpiresInMinutes"] = "5"
        }).Build(),
        TimeProvider.System);

    /// <summary>The bearer handler as Program.cs configures it, the gate behind it, one POST endpoint.</summary>
    private sealed class GateHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient _client;

        private GateHost(WebApplication app)
        {
            _app = app;
            _client = app.GetTestClient();
        }

        public static async Task<GateHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();

            var policy = new Mock<IMfaEnforcementPolicy>();
            policy.Setup(p => p.IsPrivilegedMfaRequiredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            builder.Services.AddSingleton(policy.Object);

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub"
                });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseMiddleware<PrivilegedMfaEnforcementMiddleware>();
            app.MapPost("/api/probe", () => Results.Ok());
            await app.StartAsync();
            return new GateHost(app);
        }

        public Task<HttpResponseMessage> PostAsync(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/probe");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return _client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
