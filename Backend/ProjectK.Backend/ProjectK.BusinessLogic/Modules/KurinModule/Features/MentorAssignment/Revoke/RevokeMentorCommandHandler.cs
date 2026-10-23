using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Revoke;

public class RevokeMentorCommandHandler : IRequestHandler<RevokeMentorCommand, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;

    public RevokeMentorCommandHandler(
        IUnitOfWork unitOfWork,
        IBackendCache cache,
        IAccessChangeSignal accessChanges,
        IMemberDirectory members,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _accessChanges = accessChanges;
        _members = members;
        _audit = audit;
    }

    public async Task<ServiceResult<bool>> Handle(RevokeMentorCommand request, CancellationToken cancellationToken)
    {
        var existingAssignment = await _unitOfWork.MentorAssignments.GetSpecificAssignmentAsync(request.MentorUserKey, request.GroupKey, cancellationToken);

        if (existingAssignment == null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "MentorAssignmentNotFound", "Mentor assignment not found.");
        }

        if (existingAssignment.RevokedAtUtc != null)
        {
            return new ServiceResult<bool>(ResultType.Success, true, "Assignment was already revoked.");
        }

        existingAssignment.RevokedAtUtc = DateTime.UtcNow;
        _unitOfWork.MentorAssignments.Update(existingAssignment, cancellationToken);

        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        var mentor = await _members.FindByAccountAsync(request.MentorUserKey, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Office,
                AuditActions.Office.MentorRevoked,
                existingAssignment.MentorAssignmentKey,
                KurinReportTerminology.LeadershipRole(LeadershipRole.Vykhovnyk),
                mentor?.MemberKey,
                AuditChanges.Removed(("group", group?.Name)),
                KurinKey: group?.KurinKey),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Revocation must take effect at once, not after the TTL — otherwise the
        // mentor keeps write access to the group until the cached set expires.
        _cache.Invalidate(BackendCachePolicies.MentorScopeReads);
        _accessChanges.MarkUser(request.MentorUserKey);

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
