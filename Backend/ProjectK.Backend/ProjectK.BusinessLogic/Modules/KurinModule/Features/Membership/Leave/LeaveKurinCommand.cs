using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Leave;

/// <summary>
/// Ends someone's membership in one kurin. It closes the row rather than deleting it: they were
/// there, and the levels, вмілості, awards and перестороги they earned there stay theirs and go
/// on saying where they came from.
/// </summary>
public sealed record LeaveKurinCommand(Guid MemberKey, Guid KurinKey) : IRequest<ServiceResult<object>>;

public sealed class LeaveKurinCommandHandler : IRequestHandler<LeaveKurinCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessChangeSignal _accessChanges;

    public LeaveKurinCommandHandler(IUnitOfWork unitOfWork, IAccessChangeSignal accessChanges)
    {
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
    }

    public async Task<ServiceResult<object>> Handle(LeaveKurinCommand request, CancellationToken cancellationToken)
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

        membership.LeftAtUtc = DateTime.UtcNow;

        // Offices are held by someone who belongs here; the access layer reads both together and
        // stops granting the office the moment the membership closes, so there is nothing else to
        // end. The office record stays as history.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (membership.UserKey is { } userKey)
        {
            _accessChanges.MarkUser(userKey);
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}
