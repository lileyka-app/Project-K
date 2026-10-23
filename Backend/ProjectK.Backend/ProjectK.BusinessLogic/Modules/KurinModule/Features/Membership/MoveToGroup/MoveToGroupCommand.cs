using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.MoveToGroup;

/// <summary>
/// Moves someone between гуртки inside one kurin, or takes them out of one altogether — belonging
/// to a kurin and to no гурток is a state a membership is allowed to be in.
/// </summary>
public sealed record MoveToGroupCommand(Guid MemberKey, Guid KurinKey, Guid? GroupKey)
    : IRequest<ServiceResult<object>>;

public sealed class MoveToGroupCommandHandler : IRequestHandler<MoveToGroupCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IDomainEventPublisher _events;

    public MoveToGroupCommandHandler(IUnitOfWork unitOfWork, IDomainEventPublisher events, IAccessChangeSignal accessChanges)
    {
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _events = events;
    }

    public async Task<ServiceResult<object>> Handle(MoveToGroupCommand request, CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty || request.KurinKey == Guid.Empty)
        {
            return ServiceResult<object>.Failure(
                ResultType.BadRequest,
                "MembershipKeysRequired",
                "Both a member and a kurin are required.");
        }

        var membership = await _unitOfWork.Memberships.GetActiveAsync(
            request.MemberKey, request.KurinKey, cancellationToken);

        if (membership is null)
        {
            return ServiceResult<object>.Failure(
                ResultType.NotFound, "NotAMember", "They do not currently belong to this kurin.");
        }

        if (request.GroupKey.HasValue)
        {
            var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
            if (group is null || group.KurinKey != request.KurinKey)
            {
                return ServiceResult<object>.Failure(
                    ResultType.BadRequest, "GroupNotInKurin", "That гурток does not belong to this kurin.");
            }
        }

        // Announced before the гурток is overwritten: whoever counts time spent in a гурток (the dues
        // ledger) has to do it while the old one is still known.
        await _events.PublishAsync(
            new MembershipMovedToGroup(membership.MembershipKey, request.KurinKey, membership.GroupKey, request.GroupKey),
            cancellationToken);

        membership.GroupKey = request.GroupKey;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // What "own гурток" reaches moves with them.
        if (membership.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}
