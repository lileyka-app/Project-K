using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

/// <param name="KraiCode">
/// The край picked on the form. Optional: without it the kurin opens in the instance's default край.
/// </param>
/// <param name="UspNumber">
/// The senior kurin's number in УСП. With <paramref name="UpsNumber"/> or alone it makes this a senior
/// kurin's application, and <paramref name="ClaimedKurinNameOrNumber"/> is left out.
/// </param>
/// <param name="UpsNumber">The senior kurin's number in УПС; see <paramref name="UspNumber"/>.</param>
public record SubmitWaitlistRegistrationCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTime DateOfBirth,
    string? Stanytsia,
    string? RegionOrCountry,
    bool IsKurinLeaderCandidate,
    string? ClaimedKurinNameOrNumber,
    bool IsPreparatoryKurin = false,
    string? KraiCode = null,
    int? UspNumber = null,
    int? UpsNumber = null) : IRequest<ServiceResult<Guid>>;

public sealed class SubmitWaitlistRegistrationCommandValidator : AbstractValidator<SubmitWaitlistRegistrationCommand>
{
    private readonly IUnitOfWork _unitOfWork;

    public SubmitWaitlistRegistrationCommandValidator(IOptions<KurinNumberOptions> numbers, IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        var range = numbers.Value;

        // Stop at the first failing rule per property so one message is returned at a time,
        // matching the handler's previous first-check-wins behaviour.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.PhoneNumber).NotEmpty().Must(PhoneNumbers.IsValid).WithMessage(PhoneNumbers.InvalidMessage);

        RuleFor(command => command.Stanytsia)
            .NotEmpty().WithMessage("Stanytsia is required.")
            .MaximumLength(120).WithMessage("Stanytsia must be 120 characters or fewer.");

        // The form picks a край from the list; the free-text region is what older forms sent instead.
        RuleFor(command => command.RegionOrCountry)
            .NotEmpty().WithMessage("Region is required.")
            .When(command => string.IsNullOrWhiteSpace(command.KraiCode));
        RuleFor(command => command.RegionOrCountry)
            .MaximumLength(120).WithMessage("Region must be 120 characters or fewer.");

        When(command => !string.IsNullOrWhiteSpace(command.KraiCode), () =>
        {
            RuleFor(command => command.KraiCode)
                .MustAsync(KraiExistsAsync).WithErrorCode(AuthErrors.KraiNotFound).WithMessage("Такого краю немає в списку.");
        });

        RuleFor(command => command.IsKurinLeaderCandidate)
            .Equal(true).WithMessage("Kurin leader confirmation is required.");

        // A senior kurin names its number in each гілка it belongs to instead of the one УПЮ number.
        RuleFor(command => command.UspNumber)
            .Must(value => range.IsAllowed(value!.Value))
            .WithMessage($"Kurin number must be between {KurinNumberOptions.MinNumber} and {range.MaxNumber}.")
            .When(command => command.UspNumber.HasValue);
        RuleFor(command => command.UpsNumber)
            .Must(value => range.IsAllowed(value!.Value))
            .WithMessage($"Kurin number must be between {KurinNumberOptions.MinNumber} and {range.MaxNumber}.")
            .When(command => command.UpsNumber.HasValue);

        // A підготовчий курінь has no number yet, so the box stands in for it.
        When(command => !command.IsPreparatoryKurin && !command.UspNumber.HasValue && !command.UpsNumber.HasValue, () =>
        {
            RuleFor(command => command.ClaimedKurinNameOrNumber)
                .NotEmpty().WithMessage("Kurin number is required.")
                .Must(BeNumeric).WithMessage("Kurin number must contain only digits.")
                .Must(value => range.IsAllowed(int.Parse(value!.Trim())))
                .WithMessage($"Kurin number must be between {KurinNumberOptions.MinNumber} and {range.MaxNumber}.");
        });
    }

    private Task<bool> KraiExistsAsync(string? code, CancellationToken cancellationToken)
        => _unitOfWork.Krais.ExistsAsync(Krai.NormalizeCode(code)!, cancellationToken);

    private static bool BeNumeric(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed)
               && trimmed.All(char.IsDigit)
               && int.TryParse(trimmed, out _);
    }
}

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
        var isSenior = request.UspNumber.HasValue || request.UpsNumber.HasValue;
        var claimedKurinNumber = request.IsPreparatoryKurin || isSenior ? null : request.ClaimedKurinNameOrNumber?.Trim();

        // A rejected entry does not hold the address: the applicant may come back with a corrected
        // application, which then takes the old entry's place instead of waiting out its cleanup.
        var existingEntry = await _unitOfWork.WaitlistEntries.GetByEmailAsync(request.Email, cancellationToken);
        if (existingEntry != null && existingEntry.VerificationStatus != WaitlistVerificationStatus.Rejected)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, AuthErrors.WaitlistEntryExists, "Waitlist entry with this email already exists.");
        }

        // Also check existing members just in case
        if (await _members.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, AuthErrors.MemberEmailExists, "A member with this email already exists in the system.");
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
        entry.IsPreparatoryKurin = request.IsPreparatoryKurin && !isSenior;
        entry.ClaimedUspNumber = request.UspNumber;
        entry.ClaimedUpsNumber = request.UpsNumber;
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
            var kurin = entry.IsSeniorKurin
                ? KurinBranchNames.DescribeSenior(entry.ClaimedSeniorBranches)
                : entry.IsPreparatoryKurin ? "підготовчий курінь"
                : string.IsNullOrWhiteSpace(entry.ClaimedKurinNameOrNumber) ? null : $"курінь {entry.ClaimedKurinNameOrNumber}";

            await _notifications.NotifyManyAsync(
                admins.Select(admin => new NotificationRequest
                {
                    RecipientUserKey = admin.Id,
                    Type = AppNotificationType.WaitlistEntrySubmitted,
                    Title = "Нова заявка на розгляд",
                    Body = kurin is null ? applicant : $"{applicant} · {kurin}",
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
