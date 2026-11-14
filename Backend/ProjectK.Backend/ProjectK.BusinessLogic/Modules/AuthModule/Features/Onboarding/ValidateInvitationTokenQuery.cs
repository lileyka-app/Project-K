using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

public record ValidateInvitationTokenQuery(string Token) : IRequest<ServiceResult<InvitationValidationDto>>;

public class ValidateInvitationTokenQueryHandler : IRequestHandler<ValidateInvitationTokenQuery, ServiceResult<InvitationValidationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ValidateInvitationTokenQueryHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<InvitationValidationDto>> Handle(ValidateInvitationTokenQuery request, CancellationToken cancellationToken)
    {
        var invitation = await _unitOfWork.Invitations.GetByTokenAsync(request.Token, cancellationToken);

        if (invitation == null || invitation.ExpiresAtUtc < _timeProvider.GetUtcNow().UtcDateTime)
        {
            // The placeholder response carried no information; the message is what the activation
            // page shows, and it was being dropped into CreatedAtActionName.
            return ServiceResult<InvitationValidationDto>.Failure(
                ResultType.NotFound,
                AuthErrors.InvalidInvitationToken,
                "Invalid or expired invitation token.");
        }

        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(invitation.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            // The placeholder response carried no information; the message is what the activation
            // page shows, and it was being dropped into CreatedAtActionName.
            return ServiceResult<InvitationValidationDto>.Failure(
                ResultType.NotFound,
                AuthErrors.WaitlistEntryNotFound,
                "Associated waitlist entry not found.");
        }

        return new ServiceResult<InvitationValidationDto>(
            ResultType.Success,
            new InvitationValidationDto(entry.Email, entry.FirstName, entry.LastName, true));
    }
}
