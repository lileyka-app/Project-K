using MediatR;
using ProjectK.BusinessLogic.Modules.AuditModule.Models;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuditModule.Features.Journal.Get;

/// <summary>
/// A page of the kurin's journal, as far as the kurin lets the caller read it, newest first. The
/// period runs from <see cref="FromUtc"/> inclusive to <see cref="ToUtc"/> exclusive.
/// </summary>
public sealed record GetKurinJournalQuery(
    Guid KurinKey,
    AuditArea? Area,
    Guid? MemberKey,
    Guid? ActorUserKey,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Skip,
    int Take) : IRequest<ServiceResult<KurinJournalPageResponse>>
{
    /// <summary>The largest page.</summary>
    public const int MaxTake = 50;
}

public sealed class GetKurinJournalQueryHandler : IRequestHandler<GetKurinJournalQuery, ServiceResult<KurinJournalPageResponse>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IMemberDirectory _members;
    private readonly IKurinJournalReader _journal;

    public GetKurinJournalQueryHandler(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IResourceScopeReader scopeReader,
        IMemberDirectory members,
        IKurinJournalReader journal)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _members = members;
        _journal = journal;
    }

    public async Task<ServiceResult<KurinJournalPageResponse>> Handle(GetKurinJournalQuery request, CancellationToken cancellationToken)
    {
        // The reach is the policy of the kurin the token acts in; it says nothing about another one.
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<KurinJournalPageResponse>.Failure(
                ResultType.Forbidden,
                "JournalOutsideKurin",
                "The journal can only be read in the kurin the session acts in.");
        }

        var reach = await _permissions.ReachAsync(KurinCapability.ReadJournal, cancellationToken);
        if (reach == CapabilityReach.None)
        {
            return ServiceResult<KurinJournalPageResponse>.Failure(
                ResultType.Forbidden,
                "JournalReadNotAllowed",
                "The kurin's policy does not let you read the journal.");
        }

        JournalVisibility? visibility = null;
        if (reach == CapabilityReach.OwnGroup)
        {
            if (_currentUser.UserId is not { } userKey)
            {
                return new ServiceResult<KurinJournalPageResponse>(ResultType.Unauthorized);
            }

            var ledGroups = await _scopeReader.GetLedGroupKeysAsync(userKey, request.KurinKey, cancellationToken);
            var self = await _members.FindByAccountAsync(userKey, cancellationToken);
            visibility = new JournalVisibility(ledGroups, self?.MemberKey);
        }

        var take = Math.Clamp(request.Take, 1, GetKurinJournalQuery.MaxTake);
        var filter = new KurinJournalFilter(
            request.KurinKey,
            request.Area,
            request.MemberKey,
            request.ActorUserKey,
            request.FromUtc,
            request.ToUtc,
            visibility);

        // One more than the page, to learn whether another follows without counting them all.
        var entries = await _journal.ReadAsync(filter, Math.Max(0, request.Skip), take + 1, cancellationToken);

        return new ServiceResult<KurinJournalPageResponse>(
            ResultType.Success,
            new KurinJournalPageResponse(
                entries.Take(take).Select(KurinJournalEntryResponse.FromEntity).ToList(),
                entries.Count > take));
    }
}
