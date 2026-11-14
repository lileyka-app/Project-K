using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;

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
    private readonly IMembershipRepository _memberships;
    private readonly IKurinRepository _kurins;
    private readonly IHonoursCatalog _honours;
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;
    private readonly IResourceScopeRepository _scope;

    public UpsertMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IUnitOfWork kurinData,
        IResourceAccessService access,
        IKurinAudit audit,
        IHonoursCatalog honours)
    {
        _scope = kurinData.ResourceScopes;
        _kurins = kurinData.Kurins;
        _honours = honours;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _memberships = kurinData.Memberships;
        _access = access;
        _audit = audit;
    }

    public async Task<ServiceResult<MemberAwardDto>> Handle(UpsertMemberAwardCommand request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
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
                    ResultType.BadRequest, MemberErrors.SeniorAwardOutsideSeniorKurin, "A senior відзначення is recorded in a УСП or УПС kurin, or by a senior person on their own card.");
        }

        if (inSeniorKurin || existingAward?.CatalogId is not null)
        {
            return ServiceResult<MemberAwardDto>.Failure(
                ResultType.BadRequest, MemberErrors.YouthAwardInSeniorKurin, "An УПЮ відзначення is recorded in an УПЮ kurin.");
        }

        var action = existingAward is null ? ResourceAction.Create : ResourceAction.Update;
        if (await _access.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, action, request.MemberKey, cancellationToken: cancellationToken) is { IsAllowed: false } senior)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.Forbidden, MemberErrors.SeniorYouthRecord, senior.Reason);
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
                    MemberErrors.ConfirmedAwardEditNotAllowed,
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

        var response = existingAward.ToDto();
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
                ResultType.BadRequest, MemberErrors.UnknownSeniorAward, "The catalogue has no such senior відзначення.");
        }

        var now = DateTime.UtcNow;
        var userKey = _currentUserContext.UserId;
        if (existingAward is not null && existingAward.MemberKey == request.MemberKey)
        {
            if (existingAward.CatalogId is null)
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.BadRequest, MemberErrors.SeniorAwardOutsideSeniorKurin, "An УПЮ відзначення stays one.");
            }

            var owner = member.UserKey is { } account && account == userKey;
            if (!owner && !(await _access.CheckCapabilityAsync(KurinCapability.RemoveAward, ResourceType.Member, request.MemberKey, cancellationToken)).IsAllowed)
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.Forbidden,
                    MemberErrors.ConfirmedAwardEditNotAllowed,
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
        return new ServiceResult<MemberAwardDto>(ResultType.Success, existingAward.ToDto());
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

/// <summary>
/// A відзначення is a date and a level, and the command carried both as non-nullable value types
/// with nothing checking them. A request that simply did not mention the date answered 200 and
/// stored <see cref="DateTime.MinValue"/> — the card then read «0001». It was found by sending
/// the field under the wrong name, which is exactly how a client would meet it.
/// </summary>
public sealed class UpsertMemberAwardCommandValidator : AbstractValidator<UpsertMemberAwardCommand>
{
    /// <summary>
    /// A little slack so a провід in a zone ahead of UTC can record today's відзначення today.
    /// Without it «сьогодні» in Kyiv is the future in UTC for three hours every evening.
    /// </summary>
    private static readonly TimeSpan Tolerance = TimeSpan.FromDays(1);

    public UpsertMemberAwardCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.MemberKey).NotEmpty();

        // The enum starts at 1 precisely so that "not said" is not a valid level — see
        // MemberAwardLevel. This is what makes that intent hold at the boundary.
        // One or the other: an УПЮ відзначення by its level, a senior one by its catalogue id.
        RuleFor(command => command)
            .Must(command => (command.Level is null) != string.IsNullOrWhiteSpace(command.CatalogId))
            .WithName(nameof(UpsertMemberAwardCommand.Level))
            .WithMessage("A відзначення is either an УПЮ level or a senior one from the catalogue.");

        RuleFor(command => command.Level)
            .IsInEnum()
            .When(command => command.Level is not null)
            .WithMessage("A відзначення level is required.");

        RuleFor(command => command.CatalogId).MaximumLength(ProjectK.Common.Entities.KurinModule.MemberAward.CatalogIdMaxLength);

        RuleFor(command => command.DateAcquired)
            .NotEmpty()
            .WithMessage("The date the відзначення was earned is required.");

        RuleFor(command => command.DateAcquired)
            .Must(date => date <= timeProvider.GetUtcNow().UtcDateTime.Add(Tolerance))
            .WithMessage("A відзначення cannot be earned in the future.");

        RuleFor(command => command.Note).MaximumLength(2000);
    }
}
