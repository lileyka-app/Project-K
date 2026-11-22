using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;

/// <summary>
/// The Звʼязковий of the person's kurin answers a request. Approving «перевести» closes the
/// membership here and opens one in the asking kurin in one save; approving «прийняти ще й до нас»
/// only opens the new one. Debt does not go with the person: «перевести» waits until they owe this
/// kurin nothing for quarters already behind them.
/// </summary>
public sealed record DecideMembershipRequestCommand(Guid KurinKey, Guid MembershipRequestKey, bool Approve)
    : IRequest<ServiceResult<Unit>>;

public sealed class DecideMembershipRequestCommandHandler : IRequestHandler<DecideMembershipRequestCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly MembershipAskRouting _routing;
    private readonly IDomainEventPublisher _events;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAccessChangeStore _accessChanges;
    private readonly TimeProvider _time;

    public DecideMembershipRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        MembershipMoves moves,
        MembershipAskRouting routing,
        IDomainEventPublisher events,
        ICurrentUserContext currentUser,
        IAccessChangeStore accessChanges,
        TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _moves = moves;
        _routing = routing;
        _events = events;
        _currentUser = currentUser;
        _accessChanges = accessChanges;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(DecideMembershipRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, KurinErrors.MembershipRequestOtherKurin, "A kurin reads and answers only its own requests.");
        }

        var asked = await _unitOfWork.MembershipRequests.GetAsync(request.MembershipRequestKey, cancellationToken);
        if (asked is null || asked.FromKurinKey != request.KurinKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.RequestNotFound, "No such request to this kurin.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        if (!asked.IsOpen(now))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The request was already answered, withdrawn or has lapsed.");
        }

        var person = await _members.FindAsync(asked.MemberKey, cancellationToken);
        if (person is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.MemberNotFound, "No such member.");
        }

        if (request.Approve)
        {
            // Someone who has left since the request was sent — let go to another kurin, or released —
            // is no longer this kurin's to give.
            var here = await _unitOfWork.Memberships.GetActiveAsync(asked.MemberKey, request.KurinKey, cancellationToken);
            if (here is null)
            {
                asked.Lapse(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.MemberNotHere, "They no longer stand in this kurin.");
            }

            // A senior kurin takes nobody still in УПЮ — asked before that rule, or a ступінь withdrawn since.
            var route = await _routing.ResolveAsync(asked.MemberKey, asked.ToKurinKey, cancellationToken);
            if (route.StillYouth)
            {
                asked.Lapse(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.PersonStillYouth, "The person is still in УПЮ; a senior kurin does not take them.");
            }

            // Asked before the one-senior-kurin rule, or into a senior kurin while they moved to another.
            if (asked.Kind == MembershipRequestKind.AlsoJoin && !route.AlongsideAllowed)
            {
                asked.Lapse(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.SecondSeniorKurin, "They already stand in a senior kurin; only a transfer takes them.");
            }

            if (asked.Kind == MembershipRequestKind.Transfer
                && await _moves.OwedToLeaveAsync(asked.MemberKey, request.KurinKey, cancellationToken) > 0)
            {
                return ServiceResult<Unit>.Failure(
                    ResultType.Conflict,
                    KurinErrors.MemberOwesDues,
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
            return ServiceResult<Unit>.Failure(ResultType.Conflict, KurinErrors.RequestClosed, "The request was already answered, withdrawn or has lapsed.");
        }

        if (request.Approve && person.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
