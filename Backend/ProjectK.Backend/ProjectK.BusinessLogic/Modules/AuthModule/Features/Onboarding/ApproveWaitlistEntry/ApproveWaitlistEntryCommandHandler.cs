using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ApproveWaitlistEntry;

public class ApproveWaitlistEntryCommandHandler : IRequestHandler<ApproveWaitlistEntryCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accountProvisioning;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly KurinNumberOptions _numbers;
    private readonly KraiOptions _krai;

    public ApproveWaitlistEntryCommandHandler(
        IUnitOfWork unitOfWork,
        IAccountProvisioningService accountProvisioning,
        IEmailService emailService,
        ICurrentUserContext currentUserContext,
        IOptions<KurinNumberOptions> numbers,
        IOptions<KraiOptions> krai)
    {
        _numbers = numbers.Value;
        _krai = krai.Value;
        _unitOfWork = unitOfWork;
        _accountProvisioning = accountProvisioning;
        _emailService = emailService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<Guid>> Handle(ApproveWaitlistEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(request.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "WaitlistEntryNotFound", "Waitlist entry not found.");
        }

        if (entry.VerificationStatus == WaitlistVerificationStatus.ApprovedForInvitation)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, "WaitlistEntryAlreadyApproved", "Waitlist entry is already approved.");
        }

        // The admin's pick wins over the applicant's; an entry from before the form asked has neither,
        // and its kurin opens in the instance's default край.
        var chosenKrai = Krai.NormalizeCode(request.KraiCode);
        if (chosenKrai is not null && !await _unitOfWork.Krais.ExistsAsync(chosenKrai, cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.BadRequest, "KraiNotFound", "Такого краю немає в списку.");
        }

        var kraiCode = chosenKrai ?? entry.KraiCode ?? _krai.Default;

        // A leader's kurin opens with the number they claimed, or none when they lead a підготовчий
        // курінь. A claim outside the range (an early form let "0" through) is read the same way:
        // the number is theirs to set later in the kurin's settings.
        int? kurinNumber = null;
        if (entry.IsKurinLeaderCandidate
            && !entry.IsPreparatoryKurin
            && int.TryParse(entry.ClaimedKurinNameOrNumber?.Trim(), out var claimed)
            && _numbers.IsAllowed(claimed))
        {
            kurinNumber = claimed;
            // A number is the kurin's within its край, so it is only taken if this край has it.
            if (await _unitOfWork.Kurins.ExistsAsync(kraiCode, claimed, cancellationToken))
            {
                return ServiceResult<Guid>.Failure(ResultType.Conflict, "KurinNumberTaken", "A kurin with this number already exists in this krai.");
            }
        }

        // 2. Create the inactive account and its invitation
        var provisioned = await _accountProvisioning.ProvisionAsync(
            new AccountProvisioningRequest(
                entry.Email,
                entry.FirstName,
                entry.LastName,
                entry.WaitlistEntryKey),
            cancellationToken);

        if (provisioned.Type != ResultType.Success || provisioned.Data is null)
        {
            return ServiceResult<Guid>.Failure(
                provisioned.Type,
                provisioned.ErrorCode ?? "UserNotCreated",
                provisioned.ErrorMessage ?? "Failed to create user.");
        }

        // 3. Create Kurin Placeholder if leader candidate
        if (entry.IsKurinLeaderCandidate)
        {
            var kurin = new Kurin(kurinNumber) { KraiCode = kraiCode };
            _unitOfWork.Kurins.Create(kurin, cancellationToken);

            // The entry remembers which kurin it opened, so activation can seat the founder in it.
            // It used to be written on the account, which then looked like the place it belonged.
            entry.FoundedKurinKey = kurin.KurinKey;
        }

        // 4. Update Waitlist Entry
        entry.KraiCode = kraiCode;
        entry.VerificationStatus = WaitlistVerificationStatus.ApprovedForInvitation;
        entry.ApprovedAtUtc = DateTime.UtcNow;
        entry.ReviewedAtUtc = DateTime.UtcNow;
        entry.ReviewedByUserKey = _currentUserContext.UserId;
        entry.InvitationSentAtUtc = DateTime.UtcNow;
        _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Send Invitation Email
        await _emailService.SendInvitationEmailAsync(entry.Email, provisioned.Data.InvitationToken, cancellationToken);

        return new ServiceResult<Guid>(ResultType.Success, provisioned.Data.InvitationKey);
    }
}
