using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
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

    public UpsertMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper,
        IUnitOfWork kurinData)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _mapper = mapper;
        _memberships = kurinData.Memberships;
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
            if (existingAward.Status == BadgeProgressStatus.Confirmed && !_currentUserContext.CanLeadGroups())
            {
                return ServiceResult<MemberAwardDto>.Failure(
                    ResultType.Forbidden,
                    "ConfirmedAwardEditRequiresLeadership",
                    "Only leadership may change a confirmed award.");
            }

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
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishAwardSubmittedAsync(member, existingAward, actingKurinKey, groupKey, cancellationToken);

        var response = _mapper.Map<MemberAwardDto>(existingAward);
        return new ServiceResult<MemberAwardDto>(ResultType.Success, response);
    }

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
