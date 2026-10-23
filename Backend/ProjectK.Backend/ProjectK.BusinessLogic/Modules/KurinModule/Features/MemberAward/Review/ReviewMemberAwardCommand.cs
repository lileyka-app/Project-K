using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;

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
    private readonly IMapper _mapper;

    public ReviewMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IMemberDirectory members,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _currentUserContext = currentUserContext;
        _events = events;
        _mapper = mapper;
    }

    public async Task<ServiceResult<MemberAwardDto>> Handle(ReviewMemberAwardCommand request, CancellationToken cancellationToken)
    {
        var award = await _unitOfWork.MemberAwards.GetByKeyAsync(request.MemberAwardKey, cancellationToken);
        // The resource check authorized the route's member, not the award's: anyone else's award
        // answers as missing.
        if (award is null || award.MemberKey != request.MemberKey)
        {
            return new ServiceResult<MemberAwardDto>(ResultType.NotFound);
        }

        var fromStatus = award.Status;
        if (fromStatus != BadgeProgressStatus.Submitted)
        {
            return new ServiceResult<MemberAwardDto>(ResultType.Conflict);
        }

        // Said, because the validator refused the request otherwise.
        var approved = request.IsApproved!.Value;

        award.Status = approved ? BadgeProgressStatus.Confirmed : BadgeProgressStatus.Rejected;
        award.ReviewedAtUtc = DateTime.UtcNow;
        award.ReviewedByUserKey = _currentUserContext.UserId;
        award.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.MemberAwards.Update(award, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyMemberOwnerAsync(award, approved, cancellationToken);

        return new ServiceResult<MemberAwardDto>(ResultType.Success, _mapper.Map<MemberAwardDto>(award));
    }

    private async Task NotifyMemberOwnerAsync(
        Common.Entities.KurinModule.MemberAward award,
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
