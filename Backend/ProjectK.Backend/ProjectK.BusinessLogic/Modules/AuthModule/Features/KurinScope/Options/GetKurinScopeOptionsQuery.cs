using MediatR;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope.Options;

/// <summary>
/// Where this account may stand. Read by the toolbar before offering a switch, so that the choice
/// shown and the choice the server would allow are the same list.
/// </summary>
public sealed record GetKurinScopeOptionsQuery(Guid UserKey)
    : IRequest<ServiceResult<IReadOnlyCollection<KurinScopeOption>>>;

/// <param name="Kind">Юнацьке членство чи виховний склад — the same person may be either, in different kurins.</param>
/// <param name="Branches">Every гілка with the kurin's number there, main one first.</param>
public sealed record KurinScopeOption(
    Guid KurinKey,
    int? KurinNumber,
    KurinBranch Branch,
    string? NamedAfter,
    MembershipKind Kind,
    IReadOnlyList<KurinNumbering> Branches);

public sealed class GetKurinScopeOptionsQueryHandler
    : IRequestHandler<GetKurinScopeOptionsQuery, ServiceResult<IReadOnlyCollection<KurinScopeOption>>>
{
    private readonly IMembershipDirectory _memberships;

    public GetKurinScopeOptionsQueryHandler(IMembershipDirectory memberships)
    {
        _memberships = memberships;
    }

    public async Task<ServiceResult<IReadOnlyCollection<KurinScopeOption>>> Handle(
        GetKurinScopeOptionsQuery request,
        CancellationToken cancellationToken)
    {
        // Only current memberships: a kurin someone has left is part of their history, not a place
        // they may still act in. An admin's reach beyond this list is not membership and is not
        // offered here — that is what the admin panel is.
        var memberships = await _memberships.GetCurrentForAccountAsync(request.UserKey, cancellationToken);

        IReadOnlyCollection<KurinScopeOption> options =
        [
            .. memberships
                .OrderBy(m => m.KurinNumber)
                .Select(m => new KurinScopeOption(
                    m.KurinKey, m.KurinNumber, m.Branch, m.KurinNamedAfter, m.Kind,
                    [.. KurinBranchSet.Ordered(m.Numbering, n => n.Branch)]))
        ];

        return new ServiceResult<IReadOnlyCollection<KurinScopeOption>>(ResultType.Success, options);
    }
}
