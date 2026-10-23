using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Submit;

public sealed class SubmitBadgeProgressCommand : IRequest<ServiceResult<BadgeProgressResponse>>
{
    public SubmitBadgeProgressCommand(Guid memberKey, string badgeId, string? note)
    {
        MemberKey = memberKey;
        BadgeId = badgeId;
        Note = note;
    }

    public Guid MemberKey { get; }
    public string BadgeId { get; }
    public string? Note { get; }
}

public sealed class SubmitBadgeProgressCommandHandler : IRequestHandler<SubmitBadgeProgressCommand, ServiceResult<BadgeProgressResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IKurinCatalog _kurinCatalog;
    private readonly AuditActorResolver _actors;
    private readonly IKurinAudit _audit;
    private readonly IBadgesCatalogService _badges;

    public SubmitBadgeProgressCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IKurinCatalog kurinCatalog,
        AuditActorResolver actors,
        IKurinAudit audit,
        IBadgesCatalogService badges)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _events = events;
        _kurinCatalog = kurinCatalog;
        _actors = actors;
        _audit = audit;
        _badges = badges;
    }

    public async Task<ServiceResult<BadgeProgressResponse>> Handle(SubmitBadgeProgressCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BadgeId))
        {
            return new ServiceResult<BadgeProgressResponse>(ResultType.BadRequest);
        }

        var member = await _members.FindAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<BadgeProgressResponse>(ResultType.NotFound);
        }

        var progress = await _unitOfWork.BadgeProgresses
            .GetByMemberAndBadgeIdAsync(request.MemberKey, request.BadgeId, cancellationToken);

        if (progress?.Status == BadgeProgressStatus.Confirmed)
        {
            return new ServiceResult<BadgeProgressResponse>(ResultType.Conflict);
        }

        if (progress?.Status == BadgeProgressStatus.Submitted)
        {
            return new ServiceResult<BadgeProgressResponse>(ResultType.Success, BadgeProgressResponse.FromEntity(progress));
        }

        // Only a new start is held to the kurin's catalogue: a вмілість already in work stays the
        // member's even after the край's profile stops offering it.
        if (progress is null && !await IsOfferedAsync(member.KurinKey, request.BadgeId.Trim(), cancellationToken))
        {
            return ServiceResult<BadgeProgressResponse>.Failure(ResultType.BadRequest, "BadgeNotOffered", "The kurin's край does not offer this badge.");
        }

        var now = DateTime.UtcNow;
        var actor = await _actors.ResolveAsync(cancellationToken);
        var previousStatus = progress?.Status;

        if (progress is null)
        {
            progress = new BadgeProgress
            {
                MemberKey = request.MemberKey,
                KurinKey = member.KurinKey,
                BadgeId = request.BadgeId.Trim(),
                Status = BadgeProgressStatus.Submitted,
                SubmittedAtUtc = now,
                ReviewNote = request.Note
            };

            progress.AuditEvents.Add(new BadgeProgressAuditEvent
            {
                FromStatus = null,
                ToStatus = BadgeProgressStatus.Submitted,
                Action = "Submitted",
                ActorUserKey = actor.UserKey,
                ActorName = actor.Name,
                ActorRole = actor.Role,
                OccurredAtUtc = now,
                Note = request.Note
            });

            _unitOfWork.BadgeProgresses.Create(progress, cancellationToken);
        }
        else
        {
            progress.Status = BadgeProgressStatus.Submitted;
            progress.SubmittedAtUtc = now;
            progress.ReviewedAtUtc = null;
            progress.ReviewedByUserKey = null;
            progress.ReviewedByName = null;
            progress.ReviewedByRole = null;
            progress.ReviewNote = request.Note;

            progress.AuditEvents.Add(new BadgeProgressAuditEvent
            {
                FromStatus = previousStatus,
                ToStatus = BadgeProgressStatus.Submitted,
                Action = "Resubmitted",
                ActorUserKey = actor.UserKey,
                ActorName = actor.Name,
                ActorRole = actor.Role,
                OccurredAtUtc = now,
                Note = request.Note
            });

        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Badge,
                previousStatus is null ? AuditActions.Badge.Submitted : AuditActions.Badge.Resubmitted,
                progress.BadgeProgressKey,
                ProgressAuditLabels.Badge(_badges, progress.BadgeId),
                request.MemberKey,
                AuditChanges.Of(("status", previousStatus, progress.Status)),
                request.Note),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<BadgeProgressResponse>(ResultType.InternalServerError);
        }

        await PublishSubmittedAsync(member, progress, cancellationToken);

        return new ServiceResult<BadgeProgressResponse>(ResultType.Success, BadgeProgressResponse.FromEntity(progress));
    }

    private async Task<bool> IsOfferedAsync(Guid kurinKey, string badgeId, CancellationToken cancellationToken)
    {
        var catalog = await _kurinCatalog.ForAsync(kurinKey, cancellationToken);
        return catalog?.OffersBadge(badgeId) == true;
    }

    private async Task PublishSubmittedAsync(
        MemberSummary member,
        BadgeProgress progress,
        CancellationToken cancellationToken)
    {
        await _events.PublishAsync(
            new BadgeProgressSubmitted(
                progress.BadgeProgressKey,
                progress.BadgeId,
                member.MemberKey,
                member.FullName,
                member.KurinKey,
                member.GroupKey,
                _currentUserContext.UserId),
            cancellationToken);
    }
}
