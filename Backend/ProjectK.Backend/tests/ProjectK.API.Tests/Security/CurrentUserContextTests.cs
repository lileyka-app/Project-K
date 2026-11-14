using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectK.API.Hosting;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.Security;

public class CurrentUserContextTests
{
    [Fact]
    public void NoHttpContext_ShouldExposeAnonymousUser()
    {
        var accessor = new HttpContextAccessor();
        var context = new HttpCurrentUserContext(accessor);

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.KurinKey);
        Assert.Empty(context.Roles);
    }

    [Fact]
    public void AuthenticatedPrincipal_WithClaims_ShouldMapFields()
    {
        var userId = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("kurinKey", kurinKey.ToString()),
            new(ClaimTypes.Role, "Group.Hurtkoviy"),
            new(ClaimTypes.Role, "KV.Zvyazkovyi")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext { User = principal };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var context = new HttpCurrentUserContext(accessor);

        Assert.True(context.IsAuthenticated);
        Assert.Equal(userId, context.UserId);
        Assert.Equal(kurinKey, context.KurinKey);
        Assert.Contains("Group.Hurtkoviy", context.Roles);
        Assert.Contains("KV.Zvyazkovyi", context.Roles);
        Assert.True(context.IsInRole("Group.Hurtkoviy"));
        Assert.False(context.IsInRole("Admin"));
    }

    [Fact]
    public void InvalidGuidClaims_ShouldNotThrowAndReturnNulls()
    {
        var claims = new List<Claim>
        {
            new("sub", "not-a-guid"),
            new("kurinKey", "also-not-a-guid"),
            new(ClaimTypes.Role, "Member")
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext { User = principal };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var context = new HttpCurrentUserContext(accessor);

        Assert.True(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.KurinKey);
        Assert.Single(context.Roles);
    }
}
