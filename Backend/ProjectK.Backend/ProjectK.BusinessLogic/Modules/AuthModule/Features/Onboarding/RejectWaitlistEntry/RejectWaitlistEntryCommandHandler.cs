using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.RejectWaitlistEntry;

public class RejectWaitlistEntryCommandHandler : IRequestHandler<RejectWaitlistEntryCommand, ServiceResult<Guid>>
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
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "WaitlistEntryNotFound", "Waitlist entry not found.");
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
