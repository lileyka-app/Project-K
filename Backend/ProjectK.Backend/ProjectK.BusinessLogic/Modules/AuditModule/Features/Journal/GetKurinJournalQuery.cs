using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuditModule.Features.Journal;

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
    int Take) : IRequest<ServiceResult<KurinJournalPageDto>>
{
    /// <summary>The largest page.</summary>
    public const int MaxTake = 50;
}

public sealed class GetKurinJournalQueryHandler : IRequestHandler<GetKurinJournalQuery, ServiceResult<KurinJournalPageDto>>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IMemberDirectory _members;
    private readonly IKurinJournalReader _journal;
    private readonly IProbesCatalogService _probes;
    private readonly IBadgesCatalogService _badges;

    public GetKurinJournalQueryHandler(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IResourceScopeReader scopeReader,
        IMemberDirectory members,
        IKurinJournalReader journal,
        IProbesCatalogService probes,
        IBadgesCatalogService badges)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _members = members;
        _journal = journal;
        _probes = probes;
        _badges = badges;
    }

    public async Task<ServiceResult<KurinJournalPageDto>> Handle(GetKurinJournalQuery request, CancellationToken cancellationToken)
    {
        // The reach is the policy of the kurin the token acts in; it says nothing about another one.
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<KurinJournalPageDto>.Failure(
                ResultType.Forbidden,
                AuditErrors.JournalOutsideKurin,
                "The journal can only be read in the kurin the session acts in.");
        }

        var reach = await _permissions.ReachAsync(KurinCapability.ReadJournal, cancellationToken);
        if (reach == CapabilityReach.None)
        {
            return ServiceResult<KurinJournalPageDto>.Failure(
                ResultType.Forbidden,
                AuditErrors.JournalReadNotAllowed,
                "The kurin's policy does not let you read the journal.");
        }

        JournalVisibility? visibility = null;
        if (reach == CapabilityReach.OwnGroup)
        {
            if (_currentUser.UserId is not { } userKey)
            {
                return ServiceResult<KurinJournalPageDto>.Failure(ResultType.Unauthorized, AuditErrors.Unauthorized, "The session has no user.");
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

        return new ServiceResult<KurinJournalPageDto>(
            ResultType.Success,
            new KurinJournalPageDto(
                entries.Take(take).Select(entry => entry.ToDto(Label(entry))).ToList(),
                entries.Count > take));
    }

    // History folded in from the old probe and badge trails carries the catalogue id, not the title.
    private string Label(KurinAuditEvent entry) => entry.Area switch
    {
        AuditArea.Probe => ProgressAuditLabels.Probe(_probes, entry.SubjectLabel),
        AuditArea.Badge => ProgressAuditLabels.Badge(_badges, entry.SubjectLabel),
        _ => entry.SubjectLabel
    };
}
