using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Phones;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.InviteKurinFounder;

/// <summary>
/// Writes what approving a kurin-leader application writes, for someone who never applied: the
/// kurin, an approved queue entry that remembers it, and the pending account with its invitation.
/// Activation then takes the founder into the kurin and seats them as Зв'язковий, as it does for an
/// application — so there is one path by which a kurin gets its first Зв'язковий, not two.
/// </summary>
public sealed class InviteKurinFounderCommandHandler
    : IRequestHandler<InviteKurinFounderCommand, ServiceResult<KurinFounderInvitation>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountProvisioningService _accountProvisioning;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly TimeProvider _timeProvider;
    private readonly KraiOptions _krai;
    private readonly IBackendCache _cache;
    private readonly ILogger<InviteKurinFounderCommandHandler> _logger;

    public InviteKurinFounderCommandHandler(
        IUnitOfWork unitOfWork,
        IAccountProvisioningService accountProvisioning,
        IEmailService emailService,
        ICurrentUserContext currentUserContext,
        TimeProvider timeProvider,
        IOptions<KraiOptions> krai,
        IBackendCache cache,
        ILogger<InviteKurinFounderCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _accountProvisioning = accountProvisioning;
        _emailService = emailService;
        _currentUserContext = currentUserContext;
        _timeProvider = timeProvider;
        _krai = krai.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ServiceResult<KurinFounderInvitation>> Handle(
        InviteKurinFounderCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        // Someone who already has an account would found a second kurin through an invitation they
        // accept, not by being seated without asking; an address with an application waiting is
        // approved from the queue, where the applicant's own answers are.
        switch (await _accountProvisioning.CheckAvailabilityAsync(email, cancellationToken))
        {
            case AccountAvailability.EmailTaken:
                return ServiceResult<KurinFounderInvitation>.Failure(
                    ResultType.Conflict, "EmailTaken", "За цією адресою вже є акаунт.");
            case AccountAvailability.WaitlistPending:
                return ServiceResult<KurinFounderInvitation>.Failure(
                    ResultType.Conflict, "WaitlistPending", "Від цієї адреси вже є заявка — схвали її в «Заявках».");
        }

        var kraiCode = Krai.NormalizeCode(request.KraiCode) ?? _krai.Default;
        if (!await _unitOfWork.Krais.ExistsAsync(kraiCode, cancellationToken))
        {
            return ServiceResult<KurinFounderInvitation>.Failure(
                ResultType.BadRequest, "KraiNotFound", "Такого краю немає в списку.");
        }

        if (request.KurinNumber is int number && await _unitOfWork.Kurins.ExistsAsync(kraiCode, number, cancellationToken))
        {
            return ServiceResult<KurinFounderInvitation>.Failure(
                ResultType.Conflict, "KurinNumberTaken", "Курінь з таким числом у цьому краї вже є.");
        }

        var kurin = new Kurin(request.KurinNumber) { KraiCode = kraiCode };
        _unitOfWork.Kurins.Create(kurin, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var phoneNumber = PhoneNumbers.Normalize(request.PhoneNumber) ?? string.Empty;
        var entry = new WaitlistEntry
        {
            WaitlistEntryKey = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            DateOfBirth = now.Date,
            KraiCode = kraiCode,
            IsKurinLeaderCandidate = true,
            IsPreparatoryKurin = request.KurinNumber is null,
            ClaimedKurinNameOrNumber = request.KurinNumber?.ToString(),
            FoundedKurinKey = kurin.KurinKey,
            Source = WaitlistEntrySource.DirectAdd,
            VerificationStatus = WaitlistVerificationStatus.ApprovedForInvitation,
            RequestedAtUtc = now,
            ReviewedAtUtc = now,
            ApprovedAtUtc = now,
            ReviewedByUserKey = _currentUserContext.UserId,
            InvitationSentAtUtc = now
        };
        _unitOfWork.WaitlistEntries.Create(entry, cancellationToken);

        // Identity saves the whole context when it creates the account, so the kurin and the entry
        // go in with it or, when the account is refused, not at all.
        var provisioned = await _accountProvisioning.ProvisionAsync(
            new AccountProvisioningRequest(email, firstName, lastName, entry.WaitlistEntryKey, phoneNumber),
            cancellationToken);

        if (provisioned.Type != ResultType.Success || provisioned.Data is null)
        {
            return ServiceResult<KurinFounderInvitation>.Failure(
                provisioned.Type,
                provisioned.ErrorCode ?? "UserNotCreated",
                provisioned.ErrorMessage ?? "Failed to create user.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // The admin's list of kurins is cached; without this the new one turns up only a minute later.
        _cache.Invalidate(BackendCachePolicies.KurinReads);

        // Everything is stored by now, so a letter that does not go is reported, not failed on:
        // a retry would only meet a taken address. It goes again by address (invitation/resend,
        // or «Забув пароль», which re-sends an unclaimed invitation).
        var sent = true;
        try
        {
            await _emailService.SendInvitationEmailAsync(email, provisioned.Data.InvitationToken, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "A kurin and its founder's account were opened, but the invitation letter could not be sent.");
            sent = false;
        }

        return new ServiceResult<KurinFounderInvitation>(
            ResultType.Created,
            new KurinFounderInvitation(kurin.KurinKey, provisioned.Data.UserKey, email, sent));
    }
}
