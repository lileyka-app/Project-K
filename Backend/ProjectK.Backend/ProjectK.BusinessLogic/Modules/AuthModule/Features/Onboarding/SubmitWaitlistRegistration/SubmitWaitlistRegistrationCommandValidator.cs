using ProjectK.Common.Phones;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Options;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;

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
                .MustAsync(KraiExistsAsync).WithErrorCode("KraiNotFound").WithMessage("Такого краю немає в списку.");
        });

        RuleFor(command => command.IsKurinLeaderCandidate)
            .Equal(true).WithMessage("Kurin leader confirmation is required.");

        // A підготовчий курінь has no number yet, so the box stands in for it.
        When(command => !command.IsPreparatoryKurin, () =>
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
