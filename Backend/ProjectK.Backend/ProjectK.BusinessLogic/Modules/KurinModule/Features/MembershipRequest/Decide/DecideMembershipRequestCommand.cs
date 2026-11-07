using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Decide;

/// <summary>
/// The Звʼязковий of the person's kurin answers a request. Approving «перевести» closes the
/// membership here and opens one in the asking kurin in one save; approving «прийняти ще й до нас»
/// only opens the new one. Debt does not go with the person: «перевести» waits until they owe this
/// kurin nothing for quarters already behind them.
/// </summary>
public sealed record DecideMembershipRequestCommand(Guid KurinKey, Guid MembershipRequestKey, bool Approve)
    : IRequest<ServiceResult<object>>;

public sealed class DecideMembershipRequestCommandHandler : IRequestHandler<DecideMembershipRequestCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly TimeProvider _time;

    public DecideMembershipRequestCommandHandler(
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

    public async Task<ServiceResult<object>> Handle(DecideMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (asked is null || asked.FromKurinKey != request.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "RequestNotFound", "No such request to this kurin.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "RequestClosed", "The request was already answered, withdrawn or has lapsed.");
        }

        var person = await _members.FindAsync(asked.MemberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "MemberNotFound", "No such member.");
        }

        if (request.Approve)
        {
            // Someone who has left since the request was sent — let go to another kurin, or released —
            // is no longer this kurin's to give.
            var here = await _unitOfWork.Memberships.GetActiveAsync(asked.MemberKey, asked.FromKurinKey, cancellationToken);
            if (here is null)
            {
                asked.Lapse(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<object>.Failure(ResultType.Conflict, "MemberNotHere", "They no longer stand in this kurin.");
            }

            if (asked.Kind == MembershipRequestKind.Transfer
                && await _moves.OwedToLeaveAsync(asked.MemberKey, asked.FromKurinKey, cancellationToken) > 0)
            {
                return ServiceResult<object>.Failure(
                    ResultType.Conflict,
                    "MemberOwesDues",
                    "They owe this kurin dues; «перевести» waits until they are paid in full.");
            }

            asked.Close(MembershipRequestStatus.Approved, now, _currentUser.UserId);

            if (asked.Kind == MembershipRequestKind.Transfer)
            {
                await _moves.CloseAsync(here, person.FullName, cancellationToken);
            }

            if (await _unitOfWork.Memberships.GetActiveAsync(asked.MemberKey, asked.ToKurinKey, cancellationToken) is null)
            {
                await _moves.OpenAsync(person, asked.ToKurinKey, asked.MembershipKind, cancellationToken);
            }
        }
        else
        {
            asked.Close(MembershipRequestStatus.Declined, now, _currentUser.UserId);
        }

        // The notices save as they are written, so a second answer given at the same time is
        // refused on whichever save comes first, before anyone hears of it.
        try
        {
            await _events.PublishAsync(
                new MembershipRequestDecided(
                    asked.MembershipRequestKey, asked.Kind, asked.MemberKey, person.UserKey, person.FullName,
                    asked.FromKurinKey, asked.ToKurinKey, request.Approve, _currentUser.UserId),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (StaleRowException)
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "RequestClosed", "The request was already answered, withdrawn or has lapsed.");
        }

        if (request.Approve && person.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}
