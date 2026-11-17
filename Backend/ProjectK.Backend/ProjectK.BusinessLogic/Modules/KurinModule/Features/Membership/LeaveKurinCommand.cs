using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;

/// <summary>
/// Ends someone's membership in one kurin. It closes the row rather than deleting it: they were
/// there, and the levels, вмілості, awards and перестороги they earned there stay theirs and go
/// on saying where they came from.
/// </summary>
public sealed record LeaveKurinCommand(Guid MemberKey, Guid KurinKey) : IRequest<ServiceResult<Unit>>;

public sealed class LeaveKurinCommandHandler : IRequestHandler<LeaveKurinCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessChangeStore _accessChanges;
    private readonly IMemberDirectory _members;
    private readonly MembershipMoves _moves;
    private readonly ICurrentUserContext _currentUser;

    public LeaveKurinCommandHandler(IUnitOfWork unitOfWork, IAccessChangeStore accessChanges, IMemberDirectory members, MembershipMoves moves, ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _members = members;
        _moves = moves;
    }

    public async Task<ServiceResult<Unit>> Handle(LeaveKurinCommand request, CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty || request.KurinKey == Guid.Empty)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.BadRequest,
                KurinErrors.MembershipKeysRequired,
                "Both a member and a kurin are required.");
        }

        var membership = await _unitOfWork.Memberships.GetActiveAsync(
            request.MemberKey, request.KurinKey, cancellationToken);

        if (membership is null)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.NotFound, KurinErrors.NotAMember, "They do not currently belong to this kurin.");
        }

        if (membership.UserKey is { } holder && holder == _currentUser.UserId)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.Forbidden, KurinErrors.OwnMembership, "Nobody takes themselves out of a kurin.");
        }

        if (await _moves.OwedToLeaveAsync(request.MemberKey, request.KurinKey, cancellationToken) > 0)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.Conflict,
                KurinErrors.MemberOwesDues,
                "They owe the kurin dues; a person is released only once paid in full.");
        }

        var person = await _members.FindAsync(request.MemberKey, cancellationToken);
        await _moves.CloseAsync(membership, person?.FullName ?? string.Empty, cancellationToken);

        // Offices are held by someone who belongs here; the access layer reads both together and
        // stops granting the office the moment the membership closes, so there is nothing else to
        // end. The office record stays as history.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (membership.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
