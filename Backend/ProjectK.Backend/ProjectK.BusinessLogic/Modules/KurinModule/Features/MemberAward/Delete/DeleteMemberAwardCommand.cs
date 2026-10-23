using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;

public sealed class DeleteMemberAwardCommand : IRequest<ServiceResult<Unit>>
{
    /// <summary>The member from the route — the one the resource check let the caller act on.</summary>
    public Guid MemberKey { get; set; }

    public Guid MemberAwardKey { get; set; }
}

public sealed class DeleteMemberAwardCommandHandler : IRequestHandler<DeleteMemberAwardCommand, ServiceResult<Unit>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public DeleteMemberAwardCommandHandler(IMemberUnitOfWork unitOfWork, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
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

        // Confirming an award takes leadership, so undoing a confirmed one must too. Withdrawing
        // your own submission stays open to the member who made it.
        if (award.Status == BadgeProgressStatus.Confirmed && !_currentUser.CanLeadGroups())
        {
            return ServiceResult<Unit>.Failure(
                ResultType.Forbidden,
                "ConfirmedAwardRequiresLeadership",
                "Only leadership may remove a confirmed award.");
        }

        _unitOfWork.MemberAwards.Delete(award, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
