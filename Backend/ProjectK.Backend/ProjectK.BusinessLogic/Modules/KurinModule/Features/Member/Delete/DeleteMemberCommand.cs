using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Delete;

public class DeleteMemberCommand : IRequest<ServiceResult<object>>
{
    public Guid MemberKey { get; set; }
    public DeleteMemberCommand(Guid memberKey)
    {
        MemberKey = memberKey;
    }
}

public class DeleteMemberCommandHandler : IRequestHandler<DeleteMemberCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;
    public DeleteMemberCommandHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _audit = audit;
    }

    public async Task<ServiceResult<object>> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty)
        {
            return ServiceResult<object>.Failure(
                ResultType.BadRequest,
                "MemberKeyRequired",
                "MemberKey cannot be empty.");
        }
        var exists = await _members.ExistsAsync(request.MemberKey, cancellationToken);
        if (!exists)
        {
            return ServiceResult<object>.Failure(
                ResultType.NotFound,
                "MemberNotFound",
                $"Member with key {request.MemberKey} not found.");
        }
        // Recorded while the person can still be named and placed; their card goes with them.
        var person = await _members.FindAsync(request.MemberKey, cancellationToken);
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Member,
                AuditActions.Member.Deleted,
                request.MemberKey,
                person?.FullName ?? string.Empty,
                request.MemberKey,
                AuditChanges.Removed(("firstName", person?.FirstName), ("lastName", person?.LastName))),
            cancellationToken);

        await _unitOfWork.AgendaItems.RemoveAssignmentsForTargetsAsync([request.MemberKey], cancellationToken);
        await _members.RemoveAsync(request.MemberKey, cancellationToken);
        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<object>.Failure(
                ResultType.InternalServerError,
                "MemberDeleteFailed",
                "Failed to delete Member due to internal error.");
        }
        return new ServiceResult<object>(ResultType.Success);
    }
}
