using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Asks <see cref="IResourceScopeRepository"/> for what is stored and decides with
/// <see cref="LedGroupRules"/> and <see cref="SeniorStandingRules"/>. Every module that needs the
/// "own groups" tier or the senior line comes here, so the rule lives once and the SQL decides nothing.
/// </summary>
public sealed class ResourceScopeReader : IResourceScopeReader
{
    private readonly IResourceScopeRepository _repository;

    public ResourceScopeReader(IUnitOfWork unitOfWork)
    {
        _repository = unitOfWork.ResourceScopes;
    }

    public async Task<IReadOnlyCollection<Guid>> GetLedGroupKeysAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => LedGroupRules.LedGroupKeys(await _repository.GetLedGroupSourcesAsync(userKey, kurinKey, cancellationToken));

    public async Task<bool> StandsInSeniorKurinAsync(
        ResourceType resourceType,
        Guid resourceKey,
        CancellationToken cancellationToken = default)
        => SeniorStandingRules.StandsInSeniorKurin(
            await _repository.GetOpenMembershipBranchesAsync(resourceType, resourceKey, cancellationToken));
}
