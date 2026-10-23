using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

public class ResourceAccessServiceInstrumentationDecorator : IResourceAccessService
{
    private readonly IResourceAccessService _inner;
    private readonly ILogger<ResourceAccessServiceInstrumentationDecorator> _logger;

    public ResourceAccessServiceInstrumentationDecorator(
        IResourceAccessService inner,
        ILogger<ResourceAccessServiceInstrumentationDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        Guid resourceKey,
        CancellationToken cancellationToken = default)
        => CheckAccessAsync(resourceType, action, resourceType, resourceKey, cancellationToken);

    public async Task<ResourceAccessDecision> CheckAccessAsync(
        ResourceType resourceType,
        ResourceAction action,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Note: query-count instrumentation would ideally sit at the UnitOfWork/DbContext level;
        // this baseline logs the high-level latency per resource type.
        var decision = await _inner.CheckAccessAsync(
            resourceType, action, scopeResourceType, scopeResourceKey, cancellationToken);

        sw.Stop();

        _logger.LogInformation(
            "Access check for {ResourceType} {Action} scoped by {ScopeResourceType} {ResourceKey} took {ElapsedMs}ms. Decision: {IsAllowed}. Reason: {Reason}",
            resourceType, action, scopeResourceType, scopeResourceKey, sw.ElapsedMilliseconds, decision.IsAllowed, decision.Reason);

        return decision;
    }

    public async Task<ResourceAccessDecision> CheckCapabilityAsync(
        KurinCapability capability,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var decision = await _inner.CheckCapabilityAsync(capability, scopeResourceType, scopeResourceKey, cancellationToken);
        sw.Stop();

        _logger.LogInformation(
            "Capability check for {Capability} scoped by {ScopeResourceType} {ResourceKey} took {ElapsedMs}ms. Decision: {IsAllowed}. Reason: {Reason}",
            capability, scopeResourceType, scopeResourceKey, sw.ElapsedMilliseconds, decision.IsAllowed, decision.Reason);

        return decision;
    }

    public Task<IReadOnlySet<KurinCapability>> ReachableCapabilitiesAsync(
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        CancellationToken cancellationToken = default)
        => _inner.ReachableCapabilitiesAsync(scopeResourceType, scopeResourceKey, cancellationToken);
}
