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

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Assign;

public class AssignMentorCommandHandler : IRequestHandler<AssignMentorCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IBackendCache _cache;
    private readonly IAccessChangeSignal _accessChanges;
    private readonly IKurinAudit _audit;

    public AssignMentorCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IBackendCache cache, IAccessChangeSignal accessChanges, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _cache = cache;
        _accessChanges = accessChanges;
        _audit = audit;
    }

    public async Task<ServiceResult<Guid>> Handle(AssignMentorCommand request, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(request.GroupKey, cancellationToken);
        if (group == null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "GroupNotFound", "Group not found.");
        }

        var mentorMember = await _members.FindByAccountAsync(request.MentorUserKey, cancellationToken);
        if (mentorMember == null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "MentorNotFound", "Mentor member profile not found.");
        }

        // A person can belong to several kurins, and the summary above names only the newest one.
        // What matters is a membership in this гурток's kurin, wherever else they are.
        var belongsHere = await _members.GetByKurinAsync(
            group.KurinKey,
            new MemberSelection([], [], [request.MentorUserKey]),
            cancellationToken);
        if (belongsHere.Count == 0)
        {
            return ServiceResult<Guid>.Failure(ResultType.Forbidden, "MentorFromAnotherKurin", "Mentor must belong to the same Kurin as the Group.");
        }

        var existingAssignment = await _unitOfWork.MentorAssignments.GetSpecificAssignmentAsync(request.MentorUserKey, request.GroupKey, cancellationToken);
        if (existingAssignment != null && existingAssignment.RevokedAtUtc == null)
        {
            // The existing key used to ride along as Data, but it landed in CreatedAtActionName and the
            // client never read it; the message is what the caller needs.
            return ServiceResult<Guid>.Failure(
                ResultType.Conflict,
                "MentorAlreadyAssigned",
                "Mentor is already assigned to this group.");
        }

        var assignment = new ProjectK.Common.Entities.KurinModule.MentorAssignment
        {
            MentorAssignmentKey = Guid.NewGuid(),
            MentorUserKey = request.MentorUserKey,
            GroupKey = request.GroupKey,
            AssignedAtUtc = DateTime.UtcNow
        };

        _unitOfWork.MentorAssignments.Create(assignment, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Office,
                AuditActions.Office.MentorAssigned,
                assignment.MentorAssignmentKey,
                KurinReportTerminology.LeadershipRole(LeadershipRole.Vykhovnyk),
                mentorMember.MemberKey,
                AuditChanges.Added(("group", group.Name)),
                KurinKey: group.KurinKey),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The assignment itself is what grants гуртковий access — it is read when access is
        // decided. Only the cached scope set has to go, so the next check sees it at once.
        _cache.Invalidate(BackendCachePolicies.MentorScopeReads);
        _accessChanges.MarkUser(request.MentorUserKey);

        return new ServiceResult<Guid>(ResultType.Success, assignment.MentorAssignmentKey);
    }
}
