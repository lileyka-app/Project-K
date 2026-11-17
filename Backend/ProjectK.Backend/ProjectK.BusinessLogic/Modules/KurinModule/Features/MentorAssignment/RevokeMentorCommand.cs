using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;

public sealed record RevokeMentorCommand(Guid MentorUserKey, Guid GroupKey) : IRequest<ServiceResult<Unit>>;

public sealed class RevokeMentorCommandHandler : IRequestHandler<RevokeMentorCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly IAccessChangeStore _accessChanges;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;

    public RevokeMentorCommandHandler(
        IUnitOfWork unitOfWork,
        IBackendCache cache,
        IAccessChangeStore accessChanges,
        IMemberDirectory members,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _accessChanges = accessChanges;
        _members = members;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(RevokeMentorCommand request, CancellationToken cancellationToken)
    {
        var existingAssignment = await _unitOfWork.MentorAssignments.GetSpecificAssignmentAsync(request.MentorUserKey, request.GroupKey, cancellationToken);

        if (existingAssignment == null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, KurinErrors.MentorAssignmentNotFound, "Mentor assignment not found.");
        }

        if (existingAssignment.RevokedAtUtc != null)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        existingAssignment.RevokedAtUtc = DateTime.UtcNow;
        _unitOfWork.MentorAssignments.Update(existingAssignment, cancellationToken);

        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        var mentor = await _members.FindByAccountAsync(request.MentorUserKey, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Office,
                AuditActionNames.Office.MentorRevoked,
                existingAssignment.MentorAssignmentKey,
                KurinReportNames.LeadershipRole(LeadershipRole.Vykhovnyk),
                mentor?.MemberKey,
                AuditChangeRules.Removed(("group", group?.Name)),
                KurinKey: group?.KurinKey),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Revocation must take effect at once, not after the TTL — otherwise the
        // mentor keeps write access to the group until the cached set expires.
        _cache.Invalidate(BackendCacheRules.MentorScopeReads);
        _accessChanges.MarkUser(request.MentorUserKey);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
