using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Revoke;

public class RevokeMentorCommandHandler : IRequestHandler<RevokeMentorCommand, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly IAccessChangeSignal _accessChanges;

    public RevokeMentorCommandHandler(IUnitOfWork unitOfWork, IBackendCache cache, IAccessChangeSignal accessChanges)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _accessChanges = accessChanges;
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Revocation must take effect at once, not after the TTL — otherwise the
        // mentor keeps write access to the group until the cached set expires.
        _cache.Invalidate(BackendCachePolicies.MentorScopeReads);
        _accessChanges.MarkUser(request.MentorUserKey);

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
