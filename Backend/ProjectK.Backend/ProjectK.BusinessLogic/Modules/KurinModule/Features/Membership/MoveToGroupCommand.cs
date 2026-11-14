using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;

/// <summary>
/// Moves someone between гуртки inside one kurin, or takes them out of one altogether — belonging
/// to a kurin and to no гурток is a state a membership is allowed to be in.
/// </summary>
public sealed record MoveToGroupCommand(Guid MemberKey, Guid KurinKey, Guid? GroupKey)
    : IRequest<ServiceResult<Unit>>;

public sealed class MoveToGroupCommandHandler : IRequestHandler<MoveToGroupCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IDomainEventPublisher _events;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;
    private readonly ICurrentUserContext _currentUser;

    public MoveToGroupCommandHandler(
        IUnitOfWork unitOfWork,
        IDomainEventPublisher events,
        IAccessChangeSignal accessChanges,
        IMemberDirectory members,
        IKurinAudit audit,
        ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _events = events;
        _members = members;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(MoveToGroupCommand request, CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty || request.KurinKey == Guid.Empty)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.BadRequest,
                MembershipErrors.MembershipKeysRequired,
                "Both a member and a kurin are required.");
        }

        var membership = await _unitOfWork.Memberships.GetActiveAsync(
            request.MemberKey, request.KurinKey, cancellationToken);

        if (membership is null)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.NotFound, MembershipErrors.NotAMember, "They do not currently belong to this kurin.");
        }

        if (membership.UserKey is { } holder && holder == _currentUser.UserId)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.Forbidden, MembershipErrors.OwnMembership, "Nobody moves themselves between гуртки.");
        }

        string? toGroup = null;
        if (request.GroupKey.HasValue)
        {
            var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
            if (group is null || group.KurinKey != request.KurinKey)
            {
                return ServiceResult<Unit>.Failure(
                    ResultType.BadRequest, MembershipErrors.GroupNotInKurin, "That гурток does not belong to this kurin.");
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

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
