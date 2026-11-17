using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using ProjectK.API.Middleware;
using ProjectK.BusinessLogic.Modules.IdentityModule;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The gate answers from the token's <c>amr</c> claim and nothing else: no account lookup, so a
/// request without the claim is one from a password-only session.
/// </summary>
public class PrivilegedMfaEnforcementMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenPrivilegedUserReadsPageDataWithoutMfa()
    {
        var context = CreateContext("/api/user/users", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Get;

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenTheTokenCarriesTheSecondFactor()
    {
        var context = CreateContext("/api/user/users", "Admin", secondFactor: true);

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    /// <summary>The bearer handler renames <c>amr</c> on the way in; the gate must read that spelling too.</summary>
    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenTheSecondFactorArrivesUnderTheMappedClaimType()
    {
        var context = CreateContext("/api/user/users", "Admin", secondFactor: null);
        context.User.AddIdentity(new ClaimsIdentity([new Claim(AccessTokenClaimNames.AuthenticationMethodsMapped, AccessTokenClaimNames.Mfa)]));

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenPrivilegedUserCallsMfaSetupEndpoint()
    {
        var context = CreateContext("/api/auth/mfa/setup", "KV.Zvyazkovyi", secondFactor: false);

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenPrivilegedUserReadsOwnAccountSettings()
    {
        var context = CreateContext("/api/user/me", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Get;

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenPrivilegedUserChecksAccessWithoutMfa()
    {
        var context = CreateContext("/api/auth/check-access", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Post;

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnForbidden_WhenPrivilegedUserUpdatesOwnProfileWithoutMfa()
    {
        var context = CreateContext("/api/user/me", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Put;

        var nextCalled = await Invoke(context, required: true);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRefuseInTheStandardShape_WithAStableCode()
    {
        var context = CreateContext("/api/user/me", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Put;

        await Invoke(context, required: true);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(IdentityErrors.PrivilegedMfaRequired, body.RootElement.GetProperty("error").GetString());
        Assert.Equal("MFA is required for privileged accounts.", body.RootElement.GetProperty("message").GetString());
    }

    /// <summary>A token minted before the claim existed says nothing — and nothing is not a second factor.</summary>
    [Fact]
    public async Task InvokeAsync_ShouldReturnForbidden_WhenTheTokenSaysNothingAboutTheSecondFactor()
    {
        var context = CreateContext("/api/user/me", "KV.Zvyazkovyi", secondFactor: null);
        context.Request.Method = HttpMethods.Put;

        var nextCalled = await Invoke(context, required: true);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenUserIsNotPrivileged()
    {
        var context = CreateContext("/api/user/users", "Member", secondFactor: false);

        var nextCalled = await Invoke(context, required: true);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinue_WhenPolicyDoesNotRequireMfa()
    {
        // e.g. self-host with enforcement disabled, or Development
        var context = CreateContext("/api/user/me", "KV.Zvyazkovyi", secondFactor: false);
        context.Request.Method = HttpMethods.Put;

        var nextCalled = await Invoke(context, required: false);

        Assert.True(nextCalled);
    }

    private static async Task<bool> Invoke(HttpContext context, bool required)
    {
        var nextCalled = false;
        var middleware = new PrivilegedMfaEnforcementMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, CreatePolicy(required).Object);
        return nextCalled;
    }

    /// <param name="secondFactor">What <c>amr</c> says; null for a token without the claim.</param>
    private static DefaultHttpContext CreateContext(string path, string role, bool? secondFactor)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        if (secondFactor is { } hasSecondFactor)
        {
            claims.Add(new Claim(
                AccessTokenClaimNames.AuthenticationMethods,
                hasSecondFactor ? AccessTokenClaimNames.Mfa : AccessTokenClaimNames.Password));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }

    private static Mock<IMfaEnforcementReader> CreatePolicy(bool required)
    {
        var mock = new Mock<IMfaEnforcementReader>();
        mock.Setup(x => x.IsPrivilegedMfaRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(required);
        return mock;
    }
}
