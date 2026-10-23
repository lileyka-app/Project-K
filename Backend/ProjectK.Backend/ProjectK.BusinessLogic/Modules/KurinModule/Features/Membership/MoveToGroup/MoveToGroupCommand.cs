using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
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
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;

    public MoveToGroupCommandHandler(
        IUnitOfWork unitOfWork,
        IDomainEventPublisher events,
        IAccessChangeSignal accessChanges,
        IMemberDirectory members,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _events = events;
        _members = members;
        _audit = audit;
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

        string? toGroup = null;
        if (request.GroupKey.HasValue)
        {
            var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
            if (group is null || group.KurinKey != request.KurinKey)
            {
                return ServiceResult<object>.Failure(
                    ResultType.BadRequest, "GroupNotInKurin", "That гурток does not belong to this kurin.");
            }

            toGroup = group.Name;
        }

        if (membership.GroupKey != request.GroupKey)
        {
            var fromGroup = membership.GroupKey is { } fromKey
                ? (await _unitOfWork.Groups.GetByKeyAsync(fromKey, cancellationToken))?.Name
                : null;
            var person = await _members.FindAsync(request.MemberKey, cancellationToken);
            await _audit.RecordAsync(
                new AuditRecord(
                    AuditArea.Membership,
                    AuditActions.Membership.MovedToGroup,
                    membership.MembershipKey,
                    person?.FullName ?? string.Empty,
                    request.MemberKey,
                    AuditChanges.Of(("group", fromGroup, toGroup)),
                    KurinKey: request.KurinKey),
                cancellationToken);
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
