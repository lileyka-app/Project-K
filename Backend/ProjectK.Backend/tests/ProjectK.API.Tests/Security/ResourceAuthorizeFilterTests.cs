using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.API.Filters;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Security;

public class ResourceAuthorizeFilterTests
{
    [Fact]
    public async Task AllowedDecision_ShouldExecuteAction()
    {
        var memberKey = Guid.NewGuid();
        var resourceAccessService = new Mock<IResourceAccessService>();
        resourceAccessService
            .Setup(service => service.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, ResourceType.Member, memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var filter = new ResourceAuthorizeFilter(
            true,
            ResourceType.Member,
            string.Empty,
            ResourceAction.Read,
            "route:memberKey",
            useScopeOverride: false,
            scopeResourceType: default,
            resourceAccessService.Object);

        var context = CreateContext(isAuthenticated: true);
        context.RouteData.Values["memberKey"] = memberKey;
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.True(executed);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task DeniedDecision_ShouldReturnForbidden()
    {
        var memberKey = Guid.NewGuid();
        var resourceAccessService = new Mock<IResourceAccessService>();
        resourceAccessService
            .Setup(service => service.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, ResourceType.Member, memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Access denied."));

        var filter = new ResourceAuthorizeFilter(
            true,
            ResourceType.Member,
            string.Empty,
            ResourceAction.Update,
            "route:memberKey",
            useScopeOverride: false,
            scopeResourceType: default,
            resourceAccessService.Object);

        var context = CreateContext(isAuthenticated: true);
        context.RouteData.Values["memberKey"] = memberKey;

        await filter.OnActionExecutionAsync(context, () =>
        {
            throw new InvalidOperationException("Action should not execute on denied access.");
        });

        var objectResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task DynamicResourceTypeSelector_ShouldMapLeadershipTypeToGroup()
    {
        var groupKey = Guid.NewGuid();
        var resourceAccessService = new Mock<IResourceAccessService>();
        resourceAccessService
            .Setup(service => service.CheckAccessAsync(ResourceType.Group, ResourceAction.Read, ResourceType.Group, groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var filter = new ResourceAuthorizeFilter(
            false,
            default,
            "route:leadershipType",
            ResourceAction.Read,
            "route:typeKey",
            useScopeOverride: false,
            scopeResourceType: default,
            resourceAccessService.Object);

        var context = CreateContext(isAuthenticated: true);
        context.RouteData.Values["leadershipType"] = "group";
        context.RouteData.Values["typeKey"] = groupKey;
        var executed = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.True(executed);
        resourceAccessService.Verify(
            service => service.CheckAccessAsync(ResourceType.Group, ResourceAction.Read, ResourceType.Group, groupKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidSelector_ShouldReturnBadRequest()
    {
        var resourceAccessService = new Mock<IResourceAccessService>();
        var filter = new ResourceAuthorizeFilter(
            true,
            ResourceType.Member,
            string.Empty,
            ResourceAction.Read,
            "route:memberKey",
            useScopeOverride: false,
            scopeResourceType: default,
            resourceAccessService.Object);

        var context = CreateContext(isAuthenticated: true);

        await filter.OnActionExecutionAsync(context, () =>
        {
            throw new InvalidOperationException("Action should not execute for bad request selector.");
        });

        var objectResult = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    private static ActionExecutingContext CreateContext(bool isAuthenticated)
    {
        var httpContext = new DefaultHttpContext();
        if (isAuthenticated)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                    authenticationType: "Test"));
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }
}
