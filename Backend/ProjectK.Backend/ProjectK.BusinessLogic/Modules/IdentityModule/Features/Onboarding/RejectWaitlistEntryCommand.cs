using MediatR;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

public sealed record RejectWaitlistEntryCommand(Guid WaitlistEntryKey, string? Note) : IRequest<ServiceResult<Guid>>;

public sealed class RejectWaitlistEntryCommandHandler : IRequestHandler<RejectWaitlistEntryCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public RejectWaitlistEntryCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<Guid>> Handle(RejectWaitlistEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(request.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, IdentityErrors.WaitlistEntryNotFound, "Waitlist entry not found.");
        }

        entry.VerificationStatus = WaitlistVerificationStatus.Rejected;
        entry.ReviewedAtUtc = DateTime.UtcNow;
        entry.ReviewedByUserKey = _currentUserContext.UserId;
        entry.VerificationNote = request.Note;

        _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Success, entry.WaitlistEntryKey);
    }
}
