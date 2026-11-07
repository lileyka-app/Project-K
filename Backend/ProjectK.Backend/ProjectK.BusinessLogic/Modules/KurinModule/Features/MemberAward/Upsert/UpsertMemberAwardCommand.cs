using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Upsert;

public sealed class UpsertMemberAwardCommand : IRequest<ServiceResult<MemberAwardDto>>
{
    public Guid? MemberAwardKey { get; set; }
    public Guid MemberKey { get; set; }
    /// <summary>An УПЮ відзначення's level, in an УПЮ kurin. Leave empty for a senior one.</summary>
    public MemberAwardLevel? Level { get; set; }

    /// <summary>A senior відзначення from the catalogue, in a УСП or УПС kurin. Leave empty for an УПЮ one.</summary>
    public string? CatalogId { get; set; }
    public DateTime DateAcquired { get; set; }
    public string? Note { get; set; }
}

public sealed class UpsertMemberAwardCommandHandler : IRequestHandler<UpsertMemberAwardCommand, ServiceResult<MemberAwardDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IMapper _mapper;
    private readonly IMembershipRepository _memberships;
    private readonly IKurinRepository _kurins;
    private readonly IHonoursCatalog _honours;
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;
    private readonly IResourceScopeReader _scope;

    public UpsertMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper,
        IUnitOfWork kurinData,
        IResourceAccessService access,
        IKurinAudit audit,
        IHonoursCatalog honours,
        IResourceScopeReader scope)
    {
        _scope = scope;
        _kurins = kurinData.Kurins;
        _honours = honours;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _mapper = mapper;
        _memberships = kurinData.Memberships;
        _access = access;
        _audit = audit;
    }

    public async Task<ServiceResult<MemberAwardDto>> Handle(UpsertMemberAwardCommand request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<MemberAwardDto>(ResultType.NotFound);
        }

        // Where the award happened: the kurin the провід is acting in. Reaching this person at
        // all already required a membership there, so it is also where they are.
        var actingKurinKey = _currentUserContext.KurinKey ?? Guid.Empty;
        var placement = await _memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken);
        var groupKey = placement.FirstOrDefault(m => m.KurinKey == actingKurinKey)?.GroupKey;

        ProjectK.Common.Entities.KurinModule.MemberAward? existingAward = null;
        if (request.MemberAwardKey.HasValue)
        {
            existingAward = await _unitOfWork.MemberAwards.GetByKeyAsync(request.MemberAwardKey.Value, cancellationToken);
        }

        // Each улад its own відзначення: levels 1–4 in an УПЮ kurin, the catalogue's in a senior one.
        var kurin = await _kurins.GetByKeyAsync(actingKurinKey, cancellationToken);
        var inSeniorKurin = kurin is not null && KurinBranchSet.IsSenior(kurin.Branch);
        if (request.CatalogId is not null)
        {
            // A senior kurin's провід records its people's; anywhere else a senior person keeps their own.
            var ownsCard = member.UserKey is { } account && account == _currentUserContext.UserId;
            return inSeniorKurin || (ownsCard && await IsSeniorAsync(member, cancellationToken))
                ? await UpsertSeniorAsync(request, member, actingKurinKey, existingAward, cancellationToken)
                : ServiceResult<MemberAwardDto>.Failure(
                    ResultType.BadRequest, "SeniorAwardOutsideSeniorKurin", "A senior відзначення is recorded in a УСП or УПС kurin, or by a senior person on their own card.");
        }

        if (inSeniorKurin || existingAward?.CatalogId is not null)
        {
            return ServiceResult<MemberAwardDto>.Failure(
                ResultType.BadRequest, "YouthAwardInSeniorKurin", "An УПЮ відзначення is recorded in an УПЮ kurin.");
        }

        var action = existingAward is null ? ResourceAction.Create : ResourceAction.Update;
        if (await _access.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, action, request.MemberKey, cancellationToken: cancellationToken) is { IsAllowed: false } senior)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.Forbidden, "SeniorYouthRecord", senior.Reason);
        }

        if (existingAward != null && existingAward.MemberKey == request.MemberKey)
        {
            // An edit sends the award back for review, so editing a confirmed one un-confirms it:
            // the same bar as removing it.
            if (existingAward.Status == BadgeProgressStatus.Confirmed
                && !(await _access.CheckCapabilityAsync(KurinCapability.RemoveAward, ResourceType.Member, request.MemberKey, cancellationToken)).IsAllowed)
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.Forbidden,
                    "ConfirmedAwardEditNotAllowed",
                    "Only those the kurin lets remove awards may change a confirmed award.");
            }

            var before = (existingAward.Level, existingAward.DateAcquired, existingAward.Note, existingAward.Status);

            existingAward.Level = request.Level;
            existingAward.KurinKey = actingKurinKey;
            existingAward.DateAcquired = request.DateAcquired;
            existingAward.Note = request.Note;
            existingAward.Status = BadgeProgressStatus.Submitted;
            existingAward.SubmittedAtUtc = DateTime.UtcNow;
            existingAward.SubmittedByUserKey = _currentUserContext.UserId;
            existingAward.ReviewedAtUtc = null;
            existingAward.ReviewedByUserKey = null;
            existingAward.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.MemberAwards.Update(existingAward, cancellationToken);

            var changes = AuditChanges.Of(
                ("level", before.Level, existingAward.Level),
                ("dateAcquired", before.DateAcquired, existingAward.DateAcquired),
                ("note", before.Note, existingAward.Note),
                ("status", before.Status, existingAward.Status));
            if (changes.Count > 0)
            {
                await RecordAsync(AuditActions.Award.Changed, existingAward, changes, cancellationToken);
            }
        }
        else
        {
            var newAward = new ProjectK.Common.Entities.KurinModule.MemberAward
            {
                MemberKey = request.MemberKey,
                KurinKey = actingKurinKey,
                Level = request.Level,
                DateAcquired = request.DateAcquired,
                Note = request.Note,
                Status = BadgeProgressStatus.Submitted,
                SubmittedAtUtc = DateTime.UtcNow,
                SubmittedByUserKey = _currentUserContext.UserId,
                UpdatedDate = DateTime.UtcNow
            };

            _unitOfWork.MemberAwards.Create(newAward, cancellationToken);
            existingAward = newAward;

            await RecordAsync(
                AuditActions.Award.Added,
                newAward,
                AuditChanges.Added(
                    ("level", newAward.Level),
                    ("dateAcquired", newAward.DateAcquired),
                    ("note", newAward.Note),
                    ("status", newAward.Status)),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishAwardSubmittedAsync(member, existingAward, actingKurinKey, groupKey, cancellationToken);

        var response = _mapper.Map<MemberAwardDto>(existingAward);
        return new ServiceResult<MemberAwardDto>(ResultType.Success, response);
    }

    /// <summary>
    /// A senior відзначення: the person records their own, or the senior kurin's провід does, and it
    /// stands at once — there is nobody to confirm it. Changing one takes being its owner or the
    /// kurin's right to remove awards, as removing it does.
    /// </summary>
    private async Task<ServiceResult<MemberAwardDto>> UpsertSeniorAsync(
        UpsertMemberAwardCommand request,
        MemberEntity member,
        Guid actingKurinKey,
        ProjectK.Common.Entities.KurinModule.MemberAward? existingAward,
        CancellationToken cancellationToken)
    {
        var item = _honours.GetById(request.CatalogId!.Trim());
        if (item is not { Kind: HonourKind.Award })
        {
            return ServiceResult<MemberAwardDto>.Failure(
                ResultType.BadRequest, "UnknownSeniorAward", "The catalogue has no such senior відзначення.");
        }

        var now = DateTime.UtcNow;
        var userKey = _currentUserContext.UserId;
        if (existingAward is not null && existingAward.MemberKey == request.MemberKey)
        {
            if (existingAward.CatalogId is null)
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.BadRequest, "SeniorAwardOutsideSeniorKurin", "An УПЮ відзначення stays one.");
            }

            var owner = member.UserKey is { } account && account == userKey;
            if (!owner && !(await _access.CheckCapabilityAsync(KurinCapability.RemoveAward, ResourceType.Member, request.MemberKey, cancellationToken)).IsAllowed)
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.Forbidden,
                    "ConfirmedAwardEditNotAllowed",
                    "Only the person or those the kurin lets remove awards may change a senior відзначення.");
            }

            var before = (existingAward.CatalogId, existingAward.DateAcquired, existingAward.Note);
            existingAward.CatalogId = item.Id;
            existingAward.DateAcquired = request.DateAcquired;
            existingAward.Note = request.Note;
            existingAward.UpdatedDate = now;
            _unitOfWork.MemberAwards.Update(existingAward, cancellationToken);

            var changes = AuditChanges.Of(
                ("award", HonourTitle(before.CatalogId), HonourTitle(existingAward.CatalogId)),
                ("dateAcquired", before.DateAcquired, existingAward.DateAcquired),
                ("note", before.Note, existingAward.Note));
            if (changes.Count > 0)
            {
                await RecordAsync(AuditActions.Award.Changed, existingAward, changes, cancellationToken);
            }
        }
        else
        {
            existingAward = new ProjectK.Common.Entities.KurinModule.MemberAward
            {
                MemberKey = request.MemberKey,
                KurinKey = actingKurinKey,
                CatalogId = item.Id,
                DateAcquired = request.DateAcquired,
                Note = request.Note,
                Status = BadgeProgressStatus.Confirmed,
                SubmittedAtUtc = now,
                SubmittedByUserKey = userKey,
                UpdatedDate = now
            };
            _unitOfWork.MemberAwards.Create(existingAward, cancellationToken);

            await RecordAsync(
                AuditActions.Award.Added,
                existingAward,
                AuditChanges.Added(
                    ("award", HonourTitle(existingAward.CatalogId)),
                    ("dateAcquired", existingAward.DateAcquired),
                    ("note", existingAward.Note)),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MemberAwardDto>(ResultType.Success, _mapper.Map<MemberAwardDto>(existingAward));
    }

    /// <summary>The journal keeps the catalogue's name, not its id.</summary>
    private string? HonourTitle(string? catalogId) =>
        catalogId is null ? null : _honours.GetById(catalogId)?.Title ?? catalogId;

    /// <summary>
    /// A senior person: their current ступінь, the highest on the whole ladder, is старший пластун or
    /// a сеніор's, or they stand in a УСП or УПС kurin. A сеніор who leads an УПЮ kurin is one.
    /// </summary>
    private async Task<bool> IsSeniorAsync(MemberEntity member, CancellationToken cancellationToken)
    {
        var levels = member.PlastLevelHistory.Select(history => history.PlastLevel).ToList();
        if (member.LatestPlastLevel is { } latest)
        {
            levels.Add(latest);
        }

        return PlastLadder.Highest(levels) is { } current && PlastLadder.BranchOf(current) != KurinBranch.UPYu
               || await _scope.StandsInSeniorKurinAsync(ResourceType.Member, member.MemberKey, cancellationToken);
    }

    private Task RecordAsync(
        string action,
        ProjectK.Common.Entities.KurinModule.MemberAward award,
        IReadOnlyList<AuditChange> changes,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Award,
                action,
                award.MemberAwardKey,
                KurinReportTerminology.Award(award.Level, award.CatalogId, id => _honours.GetById(id)?.Title),
                award.MemberKey,
                changes),
            cancellationToken);

    private async Task PublishAwardSubmittedAsync(
        MemberEntity member,
        ProjectK.Common.Entities.KurinModule.MemberAward award,
        Guid kurinKey,
        Guid? groupKey,
        CancellationToken cancellationToken)
    {
        await _events.PublishAsync(
            new MemberAwardSubmitted(
                award.MemberAwardKey,
                member.MemberKey,
                $"{member.FirstName} {member.LastName}".Trim(),
                kurinKey,
                groupKey,
                _currentUserContext.UserId),
            cancellationToken);
    }
}
