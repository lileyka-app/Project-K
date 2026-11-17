using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.IdentityModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberAward;

public sealed class ReviewMemberAwardCommand : IRequest<ServiceResult<MemberAwardDto>>
{
    /// <summary>The member from the route — the one the resource check let the caller act on.</summary>
    public Guid MemberKey { get; set; }

    public Guid MemberAwardKey { get; set; }

    /// <summary>Null means the caller did not say; the validator refuses it. See the request DTO.</summary>
    public bool? IsApproved { get; set; }
}

public sealed class ReviewMemberAwardCommandHandler : IRequestHandler<ReviewMemberAwardCommand, ServiceResult<MemberAwardDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;

    public ReviewMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IResourceAccessService access,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _access = access;
        _audit = audit;
        _currentUserContext = currentUserContext;
        _events = events;
    }

    public async Task<ServiceResult<MemberAwardDto>> Handle(ReviewMemberAwardCommand request, CancellationToken cancellationToken)
    {
        var award = await _unitOfWork.MemberAwards.GetByKeyAsync(request.MemberAwardKey, cancellationToken);
        // The resource check authorized the route's member, not the award's: anyone else's award
        // answers as missing.
        if (award is null || award.MemberKey != request.MemberKey)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.NotFound, MemberErrors.AwardNotFound, "No such award on this member.");
        }

        var fromStatus = award.Status;
        // A senior відзначення has no level and nobody confirms it: it stands as recorded.
        if (fromStatus != BadgeProgressStatus.Submitted || award.Level is not { } awardLevel)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.Conflict, MemberErrors.AwardNotSubmitted, "Only a submitted youth award is reviewed.");
        }

        if (await _access.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Update, award.MemberKey, cancellationToken: cancellationToken, capability: KurinCapabilityRules.ConfirmAward(awardLevel)) is { IsAllowed: false } senior)
        {
            return ServiceResult<MemberAwardDto>.Failure(ResultType.Forbidden, MemberErrors.SeniorYouthRecord, senior.Reason);
        }

        // The permission at the endpoint is shared by every level; which levels this person may
        // confirm is the kurin's policy.
        var level = await _access.CheckCapabilityAsync(
            KurinCapabilityRules.ConfirmAward(awardLevel), ResourceType.Member, award.MemberKey, cancellationToken);
        if (!level.IsAllowed)
        {
            return ServiceResult<MemberAwardDto>.Failure(
                ResultType.Forbidden,
                MemberErrors.AwardLevelNotAllowed,
                "The kurin's policy does not let you confirm an award of this level.");
        }

        // Said, because the validator refused the request otherwise.
        var approved = request.IsApproved!.Value;

        award.Status = approved ? BadgeProgressStatus.Confirmed : BadgeProgressStatus.Rejected;
        award.ReviewedAtUtc = DateTime.UtcNow;
        award.ReviewedByUserKey = _currentUserContext.UserId;
        award.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.MemberAwards.Update(award, cancellationToken);

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Award,
                approved ? AuditActionNames.Award.Confirmed : AuditActionNames.Award.Rejected,
                award.MemberAwardKey,
                KurinReportNames.AwardLevel(awardLevel),
                award.MemberKey,
                AuditChangeRules.Of(("status", fromStatus, award.Status))),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyMemberOwnerAsync(award, approved, cancellationToken);

        return new ServiceResult<MemberAwardDto>(ResultType.Success, award.ToDto());
    }

    private async Task NotifyMemberOwnerAsync(
        Common.Entities.MemberModule.MemberAward award,
        bool isApproved,
        CancellationToken cancellationToken)
    {
        var ownerUserKey = await _members.FindAccountKeyAsync(award.MemberKey, cancellationToken);
        if (ownerUserKey is null)
        {
            return;
        }

        await _events.PublishAsync(
            new MemberAwardReviewed(
                award.MemberAwardKey,
                award.MemberKey,
                ownerUserKey.Value,
                isApproved,
                _currentUserContext.UserId),
            cancellationToken);
    }
}

/// <summary>
/// A review has to say which way it went. Left as a plain <c>bool</c> this defaulted to
/// <c>false</c>, so a body that never mentioned the verdict quietly refused the відзначення.
/// </summary>
public sealed class ReviewMemberAwardCommandValidator : AbstractValidator<ReviewMemberAwardCommand>
{
    public ReviewMemberAwardCommandValidator()
    {
        RuleFor(command => command.MemberAwardKey).NotEmpty();
        RuleFor(command => command.IsApproved)
            .NotNull()
            .WithMessage("Say whether the відзначення is confirmed or refused.");
    }
}
