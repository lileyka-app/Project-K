using FluentValidation;
using Microsoft.Extensions.Options;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Phones;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.InviteKurinFounder;

public sealed class InviteKurinFounderCommandValidator : AbstractValidator<InviteKurinFounderCommand>
{
    public InviteKurinFounderCommandValidator(IOptions<KurinNumberOptions> numbers)
    {
        var range = numbers.Value;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.PhoneNumber)
            .Must(PhoneNumbers.IsValid).WithMessage(PhoneNumbers.InvalidMessage)
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));

        // No number opens a підготовчий курінь, as an approved application without one does.
        RuleFor(command => command.KurinNumber)
            .Must(number => range.IsAllowed(number!.Value))
            .WithMessage($"Kurin number must be between {KurinNumberOptions.MinNumber} and {range.MaxNumber}.")
            .When(command => command.KurinNumber.HasValue);
    }
}
