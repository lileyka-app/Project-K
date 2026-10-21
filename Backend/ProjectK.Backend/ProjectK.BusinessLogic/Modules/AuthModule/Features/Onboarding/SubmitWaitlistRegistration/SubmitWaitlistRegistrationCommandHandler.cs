using ProjectK.Common.Phones;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;

public class SubmitWaitlistRegistrationCommandHandler : IRequestHandler<SubmitWaitlistRegistrationCommand, ServiceResult<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notifications;
    private readonly ILogger<SubmitWaitlistRegistrationCommandHandler> _logger;

    public SubmitWaitlistRegistrationCommandHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        UserManager<AppUser> userManager,
        IEmailService emailService,
        INotificationService notifications,
        ILogger<SubmitWaitlistRegistrationCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _userManager = userManager;
        _emailService = emailService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<Guid>> Handle(SubmitWaitlistRegistrationCommand request, CancellationToken cancellationToken)
    {
        // Input validation lives in SubmitWaitlistRegistrationCommandValidator (runs in the pipeline).
        var stanytsia = request.Stanytsia?.Trim();
        var regionOrCountry = string.IsNullOrWhiteSpace(request.RegionOrCountry) ? null : request.RegionOrCountry.Trim();
        var kraiCode = Krai.NormalizeCode(request.KraiCode);
        var claimedKurinNumber = request.IsPreparatoryKurin ? null : request.ClaimedKurinNameOrNumber?.Trim();

        // A rejected entry does not hold the address: the applicant may come back with a corrected
        // application, which then takes the old entry's place instead of waiting out its cleanup.
        var existingEntry = await _unitOfWork.WaitlistEntries.GetByEmailAsync(request.Email, cancellationToken);
        if (existingEntry != null && existingEntry.VerificationStatus != WaitlistVerificationStatus.Rejected)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, "WaitlistEntryExists", "Waitlist entry with this email already exists.");
        }

        // Also check existing members just in case
        if (await _members.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, "MemberEmailExists", "A member with this email already exists in the system.");
        }

        var entry = existingEntry ?? new WaitlistEntry { WaitlistEntryKey = Guid.NewGuid() };
        entry.FirstName = request.FirstName;
        entry.LastName = request.LastName;
        entry.Email = request.Email;
        entry.PhoneNumber = PhoneNumbers.Normalize(request.PhoneNumber) ?? request.PhoneNumber;
        entry.DateOfBirth = request.DateOfBirth;
        entry.Stanytsia = stanytsia;
        entry.RegionOrCountry = regionOrCountry;
        entry.KraiCode = kraiCode;
        entry.IsKurinLeaderCandidate = request.IsKurinLeaderCandidate;
        entry.ClaimedKurinNameOrNumber = claimedKurinNumber;
        entry.IsPreparatoryKurin = request.IsPreparatoryKurin;
        entry.Source = WaitlistEntrySource.Application;
        entry.VerificationStatus = WaitlistVerificationStatus.Submitted;
        entry.VerificationChannel = null;
        entry.VerificationNote = null;
        entry.RequestedAtUtc = DateTime.UtcNow;
        entry.ReviewedAtUtc = null;
        entry.ReviewedByUserKey = null;

        if (existingEntry is null)
        {
            _unitOfWork.WaitlistEntries.Create(entry, cancellationToken);
        }
        else
        {
            _unitOfWork.WaitlistEntries.Update(entry, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAdministratorsAsync(entry, cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, entry.WaitlistEntryKey);
    }

    /// <summary>
    /// Every administrator hears about the entry twice: the bell in the app and a letter. Neither
    /// may fail the submission: the entry is already saved, and the applicant is not the one to
    /// tell about a mail outage.
    /// </summary>
    private async Task NotifyAdministratorsAsync(WaitlistEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var admins = await _userManager.GetUsersInRoleAsync(SystemRole.Admin);
            var applicant = $"{entry.FirstName} {entry.LastName}";
            var kurin = entry.IsPreparatoryKurin ? "підготовчий" : entry.ClaimedKurinNameOrNumber;

            await _notifications.NotifyManyAsync(
                admins.Select(admin => new NotificationRequest
                {
                    RecipientUserKey = admin.Id,
                    Type = AppNotificationType.WaitlistEntrySubmitted,
                    Title = "Нова заявка на розгляд",
                    Body = string.IsNullOrWhiteSpace(kurin) ? applicant : $"{applicant} · курінь {kurin}",
                    EntityType = "WaitlistEntry",
                    EntityKey = entry.WaitlistEntryKey,
                    Route = "/waitlist",
                    // A re-application reuses the entry, so the time keeps it from being taken for a repeat.
                    DeduplicationKey = $"waitlist-submitted:{entry.WaitlistEntryKey}:{entry.RequestedAtUtc.Ticks}:{admin.Id}"
                }),
                cancellationToken);

            foreach (var admin in admins.Where(admin => !string.IsNullOrWhiteSpace(admin.Email)))
            {
                await _emailService.SendWaitlistSubmittedEmailAsync(admin.Email!, applicant, kurin, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Waitlist entry {WaitlistEntryKey} saved, but administrators could not be told", entry.WaitlistEntryKey);
        }
    }
}
