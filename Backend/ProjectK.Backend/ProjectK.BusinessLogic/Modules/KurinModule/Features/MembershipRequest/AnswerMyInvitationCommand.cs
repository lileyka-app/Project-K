using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>
/// The person accepts or declines a kurin's invitation. Accepting only adds a membership: nothing
/// they hold elsewhere closes.
/// </summary>
public sealed record AnswerMyInvitationCommand(Guid MembershipRequestKey, bool Accept) : IRequest<ServiceResult<Unit>>;

public sealed class AnswerMyInvitationCommandHandler : IRequestHandler<AnswerMyInvitationCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentMember _currentMember;
    private readonly MembershipMoves _moves;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeStore _accessChanges;
    private readonly TimeProvider _time;

    public AnswerMyInvitationCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentMember currentMember,
        MembershipMoves moves,
        IDomainEventPublisher events,
        ICurrentUserContext currentUser,
        IAccessChangeStore accessChanges,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _currentMember = currentMember;
        _moves = moves;
        _events = events;
        _currentUser = currentUser;
        _accessChanges = accessChanges;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(AnswerMyInvitationCommand request, CancellationToken cancellationToken)
    {
        var me = await _currentMember.FindAsync(cancellationToken);
        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (me is null || asked is null || asked.FromKurinKey is not null || asked.MemberKey != me.MemberKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.RequestNotFound, "No such invitation for you.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The invitation was already answered, withdrawn or has lapsed.");
        }

        if (request.Accept)
        {
            asked.Close(MembershipRequestStatus.Approved, now, _currentUser.UserId);
            if (await _unitOfWork.Memberships.GetActiveAsync(asked.MemberKey, asked.ToKurinKey, cancellationToken) is null)
            {
                await _moves.OpenAsync(me, asked.ToKurinKey, asked.MembershipKind, cancellationToken);
            }
        }
        else
        {
            asked.Close(MembershipRequestStatus.Declined, now, _currentUser.UserId);
        }

        try
        {
            await _events.PublishAsync(
                new MembershipRequestDecided(
                    asked.MembershipRequestKey, asked.Kind, asked.MemberKey, me.UserKey, me.FullName,
                    null, asked.ToKurinKey, request.Accept, _currentUser.UserId),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (StaleRowException)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The invitation was already answered, withdrawn or has lapsed.");
        }

        if (request.Accept && me.UserKey is { } account)
        {
            _accessChanges.MarkUser(account);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
