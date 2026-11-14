using ProjectK.BusinessLogic.Modules.KurinModule;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Invitations;

/// <summary>
/// The person accepts or declines a kurin's invitation. Accepting only adds a membership: nothing
/// they hold elsewhere closes.
/// </summary>
public sealed record AnswerMyInvitationCommand(Guid MembershipRequestKey, bool Accept) : IRequest<ServiceResult<Unit>>;

public sealed class AnswerMyInvitationCommandHandler : IRequestHandler<AnswerMyInvitationCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly TimeProvider _time;

    public AnswerMyInvitationCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        MembershipMoves moves,
        IDomainEventPublisher events,
        ICurrentUserContext currentUser,
        IAccessChangeSignal accessChanges,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _moves = moves;
        _events = events;
        _currentUser = currentUser;
        _accessChanges = accessChanges;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(AnswerMyInvitationCommand request, CancellationToken cancellationToken)
    {
        var me = _currentUser.UserId is { } userKey ? await _members.FindByAccountAsync(userKey, cancellationToken) : null;
        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (me is null || asked is null || asked.FromKurinKey is not null || asked.MemberKey != me.MemberKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MembershipErrors.RequestNotFound, "No such invitation for you.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, MembershipErrors.RequestClosed, "The invitation was already answered, withdrawn or has lapsed.");
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
            return ServiceResult<Unit>.Failure(ResultType.Conflict, MembershipErrors.RequestClosed, "The invitation was already answered, withdrawn or has lapsed.");
        }

        if (request.Accept && me.UserKey is { } account)
        {
            _accessChanges.MarkUser(account);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
