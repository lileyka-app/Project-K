using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
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
    public MemberAwardLevel Level { get; set; }
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
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;

    public UpsertMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper,
        IUnitOfWork kurinData,
        IResourceAccessService access,
        IKurinAudit audit)
    {
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
                KurinReportTerminology.AwardLevel(award.Level),
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
