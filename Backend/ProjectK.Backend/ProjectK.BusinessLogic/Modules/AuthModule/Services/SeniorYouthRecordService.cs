using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The guard on the youth record of someone who stands in a УСП or УПС kurin: their probes are an
/// archive, they do not enter their own, and only the kurin-wide right changes the rest. It only
/// ever refuses, on top of the ordinary check.
/// </summary>
public sealed class SeniorYouthRecordService
{
    private const string SeniorProbe = "A senior's probes are an archive: nobody signs or changes them.";
    private const string SeniorOwnYouthRecord = "A senior does not enter their own youth record.";
    private const string SeniorYouthRecordKurinWide = "A senior's youth record is changed only by who holds the kurin-wide right where they stand now.";

    private readonly IResourceScopeReader _scopeReader;
    private readonly ResourceScopeService _scopes;

    public SeniorYouthRecordService(IResourceScopeReader scopeReader, ResourceScopeService scopes)
    {
        _scopeReader = scopeReader;
        _scopes = scopes;
    }

    /// <summary>Probes and вмілості: the youth record the guard covers through ordinary permissions.</summary>
    public static bool ChangesYouthRecord(ResourceType resourceType, ResourceAction action) =>
        action != ResourceAction.Read && resourceType is ResourceType.ProbeProgress or ResourceType.BadgeProgress;

    /// <summary>
    /// The refusal for a change to the youth record of someone who stands in a УСП or УПС kurin, or
    /// null when the change may go ahead. Former kurins never get this far: the scope check already
    /// holds them to the kurin the person stands in.
    /// </summary>
    public async Task<ResourceAccessDecision?> RefusalAsync(
        ResourceType resourceType,
        AccessScope grantedScope,
        ResourceType scopeResourceType,
        Guid scopeResourceKey,
        bool ownerMayChange,
        CancellationToken cancellationToken)
    {
        if (!await _scopeReader.StandsInSeniorKurinAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return null;
        }

        if (resourceType == ResourceType.ProbeProgress)
        {
            return ResourceAccessDecision.Deny(SeniorProbe);
        }

        if (await _scopes.IsCallersOwnAsync(scopeResourceType, scopeResourceKey, cancellationToken))
        {
            return ownerMayChange ? null : ResourceAccessDecision.Deny(SeniorOwnYouthRecord);
        }

        return grantedScope == AccessScope.KurinWide ? null : ResourceAccessDecision.Deny(SeniorYouthRecordKurinWide);
    }
}
