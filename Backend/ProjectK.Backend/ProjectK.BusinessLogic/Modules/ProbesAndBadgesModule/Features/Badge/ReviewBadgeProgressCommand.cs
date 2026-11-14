using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.ProbesAndBadgesModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;

public sealed class ReviewBadgeProgressCommand : IRequest<ServiceResult<BadgeProgressDto>>
{
    public ReviewBadgeProgressCommand(Guid memberKey, string badgeId, bool? isApproved, string? note)
    {
        MemberKey = memberKey;
        BadgeId = badgeId;
        IsApproved = isApproved;
        Note = note;
    }

    public Guid MemberKey { get; }
    public string BadgeId { get; }
    /// <summary>Null means the caller did not say; the validator refuses it. See the request DTO.</summary>
    public bool? IsApproved { get; }
    public string? Note { get; }
}

public sealed class ReviewBadgeProgressCommandHandler : IRequestHandler<ReviewBadgeProgressCommand, ServiceResult<BadgeProgressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentMember _actors;
    private readonly IKurinAudit _audit;
    private readonly IKurinJournalRepository _journal;
    private readonly IBadgesCatalogReader _badges;

    public ReviewBadgeProgressCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        ICurrentMember actors,
        IKurinAudit audit,
        IAuditUnitOfWork journal,
        IBadgesCatalogReader badges)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _events = events;
        _actors = actors;
        _audit = audit;
        _journal = journal.KurinJournal;
        _badges = badges;
    }

    public async Task<ServiceResult<BadgeProgressDto>> Handle(ReviewBadgeProgressCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BadgeId))
        {
            return ServiceResult<BadgeProgressDto>.Failure(ResultType.BadRequest, ProbesAndBadgesErrors.BadgeIdRequired, "A badge id is required.");
        }

        var progress = await _unitOfWork.BadgeProgresses
            .GetByMemberAndBadgeIdAsync(request.MemberKey, request.BadgeId, cancellationToken);

        if (progress is null)
        {
            return ServiceResult<BadgeProgressDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.BadgeProgressNotFound, "The member has not started this badge.");
        }

        var fromStatus = progress.Status;
        var canReviewSubmitted = fromStatus == BadgeProgressStatus.Submitted;
        // Said, because the validator refused the request otherwise.
        var approved = request.IsApproved!.Value;

        var canRemoveConfirmed = fromStatus == BadgeProgressStatus.Confirmed && !approved;
        if (!canReviewSubmitted && !canRemoveConfirmed)
        {
            return ServiceResult<BadgeProgressDto>.Failure(ResultType.Conflict, ProbesAndBadgesErrors.BadgeNotReviewable, "The badge is neither submitted nor confirmed.");
        }

        var now = DateTime.UtcNow;
        var actor = await _actors.ActorAsync(cancellationToken);
        var targetStatus = approved ? BadgeProgressStatus.Confirmed : BadgeProgressStatus.Rejected;

        progress.Status = targetStatus;
        progress.ReviewedAtUtc = now;
        progress.ReviewedByUserKey = actor.UserKey;
        progress.ReviewedByName = actor.Name;
        progress.ReviewedByRole = actor.Role;
        progress.ReviewNote = request.Note;

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Badge,
                approved ? AuditActions.Badge.Confirmed
                    : canRemoveConfirmed ? AuditActions.Badge.ConfirmationRemoved
                    : AuditActions.Badge.Rejected,
                progress.BadgeProgressKey,
                ProgressAuditNames.Badge(_badges, progress.BadgeId),
                progress.MemberKey,
                AuditChanges.Of(("status", fromStatus, targetStatus)),
                request.Note),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<BadgeProgressDto>.Failure(ResultType.InternalServerError, ProbesAndBadgesErrors.NotSaved, "The review was not saved.");
        }

        await NotifyMemberOwnerAsync(progress, approved, canRemoveConfirmed, cancellationToken);

        var journal = await _journal.GetForSubjectsAsync([progress.BadgeProgressKey], cancellationToken);
        return new ServiceResult<BadgeProgressDto>(ResultType.Success, BadgeMapping.ToDto(progress, journal));
    }

    private async Task NotifyMemberOwnerAsync(
        BadgeProgress progress,
        bool isApproved,
        bool removedConfirmed,
        CancellationToken cancellationToken)
    {
        var ownerUserKey = await _members.FindAccountKeyAsync(progress.MemberKey, cancellationToken);
        if (ownerUserKey is null)
        {
            return;
        }

        await _events.PublishAsync(
            new BadgeProgressReviewed(
                progress.BadgeProgressKey,
                progress.BadgeId,
                progress.MemberKey,
                ownerUserKey.Value,
                isApproved,
                removedConfirmed,
                _currentUserContext.UserId),
            cancellationToken);
    }
}

/// <summary>
/// A review has to say which way it went. Left as a plain <c>bool</c> this defaulted to
/// <c>false</c>, so a body that never mentioned the verdict quietly refused the вмілість.
/// </summary>
public sealed class ReviewBadgeProgressCommandValidator : AbstractValidator<ReviewBadgeProgressCommand>
{
    public ReviewBadgeProgressCommandValidator()
    {
        RuleFor(command => command.MemberKey).NotEmpty();
        RuleFor(command => command.BadgeId).NotEmpty();
        RuleFor(command => command.IsApproved)
            .NotNull()
            .WithMessage("Say whether the вмілість is confirmed or refused.");

    }
}
