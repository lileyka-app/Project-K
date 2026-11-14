using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

public class DeleteMemberCommand : IRequest<ServiceResult<Unit>>
{
    public Guid MemberKey { get; set; }
    public DeleteMemberCommand(Guid memberKey)
    {
        MemberKey = memberKey;
    }
}

public class DeleteMemberCommandHandler : IRequestHandler<DeleteMemberCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(DeleteMemberCommand request, CancellationToken cancellationToken)
    {
        if (request.MemberKey == Guid.Empty)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.BadRequest,
                MemberErrors.MemberKeyRequired,
                "MemberKey cannot be empty.");
        }
        var exists = await _members.ExistsAsync(request.MemberKey, cancellationToken);
        if (!exists)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.NotFound,
                MemberErrors.MemberNotFound,
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
            return ServiceResult<Unit>.Failure(
                ResultType.InternalServerError,
                MemberErrors.MemberDeleteFailed,
                "Failed to delete Member due to internal error.");
        }
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
