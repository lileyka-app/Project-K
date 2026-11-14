using MediatR;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope;

/// <summary>
/// Where this account may stand. Read by the toolbar before offering a switch, so that the choice
/// shown and the choice the server would allow are the same list.
/// </summary>
public sealed record GetKurinScopeOptionsQuery(Guid UserKey)
    : IRequest<ServiceResult<IReadOnlyCollection<KurinScopeOptionDto>>>;

public sealed class GetKurinScopeOptionsQueryHandler
    : IRequestHandler<GetKurinScopeOptionsQuery, ServiceResult<IReadOnlyCollection<KurinScopeOptionDto>>>
{
    private readonly IMembershipDirectory _memberships;

    public GetKurinScopeOptionsQueryHandler(IMembershipDirectory memberships)
    {
        _memberships = memberships;
    }

    public async Task<ServiceResult<IReadOnlyCollection<KurinScopeOptionDto>>> Handle(
        GetKurinScopeOptionsQuery request,
        CancellationToken cancellationToken)
    {
        // Only current memberships: a kurin someone has left is part of their history, not a place
        // they may still act in. An admin's reach beyond this list is not membership and is not
        // offered here — that is what the admin panel is.
        var memberships = await _memberships.GetCurrentForAccountAsync(request.UserKey, cancellationToken);

        IReadOnlyCollection<KurinScopeOptionDto> options =
        [
            .. memberships
                .OrderBy(m => m.KurinNumber)
                .Select(m => new KurinScopeOptionDto(
                    m.KurinKey, m.KurinNumber, m.Branch, m.KurinNamedAfter, m.Kind,
                    [.. KurinBranchSet.Ordered(m.Numbering, n => n.Branch)]))
        ];

        return new ServiceResult<IReadOnlyCollection<KurinScopeOptionDto>>(ResultType.Success, options);
    }
}
