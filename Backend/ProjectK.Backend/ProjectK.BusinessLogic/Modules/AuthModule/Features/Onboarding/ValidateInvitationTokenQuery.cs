using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

public record ValidateInvitationTokenQuery(string Token) : IRequest<ServiceResult<InvitationValidationResponse>>;

public record InvitationValidationResponse(string Email, string FirstName, string LastName, bool IsValid);

public class ValidateInvitationTokenQueryHandler : IRequestHandler<ValidateInvitationTokenQuery, ServiceResult<InvitationValidationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ValidateInvitationTokenQueryHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<InvitationValidationResponse>> Handle(ValidateInvitationTokenQuery request, CancellationToken cancellationToken)
    {
        var invitation = await _unitOfWork.Invitations.GetByTokenAsync(request.Token, cancellationToken);

        if (invitation == null || invitation.ExpiresAtUtc < _timeProvider.GetUtcNow().UtcDateTime)
        {
            // The placeholder response carried no information; the message is what the activation
            // page shows, and it was being dropped into CreatedAtActionName.
            return ServiceResult<InvitationValidationResponse>.Failure(
                ResultType.NotFound,
                AuthErrors.InvalidInvitationToken,
                "Invalid or expired invitation token.");
        }

        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(invitation.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            // The placeholder response carried no information; the message is what the activation
            // page shows, and it was being dropped into CreatedAtActionName.
            return ServiceResult<InvitationValidationResponse>.Failure(
                ResultType.NotFound,
                AuthErrors.WaitlistEntryNotFound,
                "Associated waitlist entry not found.");
        }

        return new ServiceResult<InvitationValidationResponse>(
            ResultType.Success,
            new InvitationValidationResponse(entry.Email, entry.FirstName, entry.LastName, true));
    }
}
