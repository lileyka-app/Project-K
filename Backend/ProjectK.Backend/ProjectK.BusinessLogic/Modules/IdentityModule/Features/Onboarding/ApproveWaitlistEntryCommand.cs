using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.KraiModule;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

/// <param name="KraiCode">
/// The край the admin settled on, overriding the applicant's pick. Without it the entry's own край
/// stands, and without that the instance's default.
/// </param>
public sealed record ApproveWaitlistEntryCommand(Guid WaitlistEntryKey, string? KraiCode = null) : IRequest<ServiceResult<WaitlistApprovalDto>>;

public sealed class ApproveWaitlistEntryCommandHandler : IRequestHandler<ApproveWaitlistEntryCommand, ServiceResult<WaitlistApprovalDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accountProvisioning;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly KurinNumberOptions _numbers;
    private readonly KraiOptions _krai;
    private readonly IBackendCache _cache;
    private readonly ILogger<ApproveWaitlistEntryCommandHandler> _logger;

    public ApproveWaitlistEntryCommandHandler(
        IUnitOfWork unitOfWork,
        IAccountProvisioningService accountProvisioning,
        IEmailService emailService,
        ICurrentUserContext currentUserContext,
        IOptions<KurinNumberOptions> numbers,
        IOptions<KraiOptions> krai,
        IBackendCache cache,
        ILogger<ApproveWaitlistEntryCommandHandler> logger)
    {
        _logger = logger;
        _numbers = numbers.Value;
        _krai = krai.Value;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _accountProvisioning = accountProvisioning;
        _emailService = emailService;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<WaitlistApprovalDto>> Handle(ApproveWaitlistEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _unitOfWork.WaitlistEntries.GetByKeyAsync(request.WaitlistEntryKey, cancellationToken);
        if (entry == null)
        {
            return ServiceResult<WaitlistApprovalDto>.Failure(ResultType.NotFound, IdentityErrors.WaitlistEntryNotFound, "Waitlist entry not found.");
        }

        if (entry.VerificationStatus == WaitlistVerificationStatus.ApprovedForInvitation)
        {
            return ServiceResult<WaitlistApprovalDto>.Failure(ResultType.Conflict, IdentityErrors.WaitlistEntryAlreadyApproved, "Waitlist entry is already approved.");
        }

        // The admin's pick wins over the applicant's; an entry from before the form asked has neither,
        // and its kurin opens in the instance's default край.
        var chosenKrai = Krai.NormalizeCode(request.KraiCode);
        if (chosenKrai is not null && !await _unitOfWork.Krais.ExistsAsync(chosenKrai, cancellationToken))
        {
            return ServiceResult<WaitlistApprovalDto>.Failure(ResultType.BadRequest, IdentityErrors.KraiNotFound, "There is no such krai.");
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
                return ServiceResult<WaitlistApprovalDto>.Failure(ResultType.Conflict, IdentityErrors.KurinNumberTaken, "A kurin with this number already exists in this krai.");
            }
        }

        // A senior kurin opens in every гілка it applied for, one kurin with a number in each.
        if (entry.IsKurinLeaderCandidate && entry.IsSeniorKurin)
        {
            foreach (var numbering in entry.ClaimedSeniorBranches)
            {
                if (await _unitOfWork.Kurins.IsNumberTakenAsync(kraiCode, numbering.Branch, numbering.Number!.Value, null, cancellationToken))
                {
                    return ServiceResult<WaitlistApprovalDto>.Failure(ResultType.Conflict, IdentityErrors.KurinNumberTaken, "A kurin with this number already exists in this krai.");
                }
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
            return ServiceResult<WaitlistApprovalDto>.Failure(
                provisioned.Type,
                provisioned.ErrorCode ?? IdentityErrors.UserNotCreated,
                provisioned.ErrorMessage ?? "Failed to create user.");
        }

        // 3. Create Kurin Placeholder if leader candidate
        if (entry.IsKurinLeaderCandidate)
        {
            var kurin = entry.IsSeniorKurin
                ? Kurin.InBranches(entry.ClaimedSeniorBranches)
                : new Kurin(kurinNumber);
            kurin.KraiCode = kraiCode;
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
        _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (entry.IsKurinLeaderCandidate)
        {
            _cache.Invalidate(BackendCacheRules.KurinReads);
        }

        // 5. Send Invitation Email. Everything is stored by now, so a letter that does not go is
        // reported, not failed on: a 500 here left the entry approved, and the admin's retry then met
        // «already approved» (409). The letter goes again from the entry («Надіслати ще раз»).
        try
        {
            await _emailService.SendInvitationEmailAsync(entry.Email, provisioned.Data.InvitationToken, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Waitlist entry {WaitlistEntryKey} was approved, but the invitation letter could not be sent.", entry.WaitlistEntryKey);
            return new ServiceResult<WaitlistApprovalDto>(ResultType.Success, new WaitlistApprovalDto(provisioned.Data.InvitationKey, InvitationSent: false));
        }

        entry.InvitationSentAtUtc = DateTime.UtcNow;
        _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<WaitlistApprovalDto>(ResultType.Success, new WaitlistApprovalDto(provisioned.Data.InvitationKey, InvitationSent: true));
    }
}
