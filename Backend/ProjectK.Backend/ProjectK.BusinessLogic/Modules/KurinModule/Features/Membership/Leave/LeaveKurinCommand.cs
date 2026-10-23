using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
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
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;

    public LeaveKurinCommandHandler(IUnitOfWork unitOfWork, IAccessChangeSignal accessChanges, IMemberDirectory members, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _accessChanges = accessChanges;
        _members = members;
        _audit = audit;
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

        // Recorded before the save, while they are still placed in the kurin and their гурток is known.
        var group = membership.GroupKey is { } groupKey
            ? await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken)
            : null;
        var person = await _members.FindAsync(request.MemberKey, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Membership,
                AuditActions.Membership.Left,
                membership.MembershipKey,
                person?.FullName ?? string.Empty,
                request.MemberKey,
                AuditChanges.Removed(("kind", membership.Kind), ("group", group?.Name)),
                KurinKey: request.KurinKey),
            cancellationToken);

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
