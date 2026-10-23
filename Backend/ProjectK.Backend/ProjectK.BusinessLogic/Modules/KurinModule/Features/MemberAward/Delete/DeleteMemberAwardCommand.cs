using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;

public sealed class DeleteMemberAwardCommand : IRequest<ServiceResult<Unit>>
{
    /// <summary>The member from the route — the one the resource check let the caller act on.</summary>
    public Guid MemberKey { get; set; }

    public Guid MemberAwardKey { get; set; }
}

/// <summary>
/// Removes an award. An unconfirmed one may be withdrawn by the person it is about or by whoever
/// submitted it; a confirmed one, or someone else's submission, takes <see cref="KurinCapability.RemoveAward"/>.
/// </summary>
public sealed class DeleteMemberAwardCommandHandler : IRequestHandler<DeleteMemberAwardCommand, ServiceResult<Unit>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;
    private readonly IResourceAccessService _access;

    public DeleteMemberAwardCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IMemberDirectory members,
        IResourceAccessService access)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _members = members;
        _access = access;
    }

    public async Task<ServiceResult<Unit>> Handle(DeleteMemberAwardCommand request, CancellationToken cancellationToken)
    {
        var award = await _unitOfWork.MemberAwards.GetByKeyAsync(request.MemberAwardKey, cancellationToken);
        // The resource check authorized the route's member, not the award's: anyone else's award
        // answers as missing.
        if (award is null || award.MemberKey != request.MemberKey)
        {
            return new ServiceResult<Unit>(ResultType.NotFound);
        }

        if (!await MayWithdrawAsync(award, cancellationToken))
        {
            var removal = await _access.CheckCapabilityAsync(
                KurinCapability.RemoveAward, ResourceType.Member, award.MemberKey, cancellationToken);
            if (!removal.IsAllowed)
            {
                return ServiceResult<Unit>.Failure(
                    ResultType.Forbidden,
                    "AwardRemovalNotAllowed",
                    "Only those the kurin lets remove awards may remove a confirmed award or someone else's submission.");
            }
        }

        _unitOfWork.MemberAwards.Delete(award, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private async Task<bool> MayWithdrawAsync(Common.Entities.KurinModule.MemberAward award, CancellationToken cancellationToken)
    {
        if (award.Status == BadgeProgressStatus.Confirmed || _currentUser.UserId is not { } userKey)
        {
            return false;
        }

        if (award.SubmittedByUserKey == userKey)
        {
            return true;
        }

        return await _members.FindAccountKeyAsync(award.MemberKey, cancellationToken) == userKey;
    }
}
