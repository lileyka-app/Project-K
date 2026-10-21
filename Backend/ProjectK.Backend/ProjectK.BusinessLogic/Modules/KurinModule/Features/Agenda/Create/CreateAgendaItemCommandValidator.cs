using FluentValidation;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Create;

public sealed class CreateAgendaItemCommandValidator : AbstractValidator<CreateAgendaItemCommand>
{
    public CreateAgendaItemCommandValidator()
    {
        RuleFor(command => command.KurinKey).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.Location).MaximumLength(200);
        RuleFor(command => command.Targets).NotEmpty().WithMessage("At least one assignment target is required.");
        RuleForEach(command => command.Targets).ChildRules(target =>
            target.RuleFor(t => t.TargetKey).NotEmpty());

        // An end without a start is meaningless, and an end must not precede a start.
        RuleFor(command => command)
            .Must(c => !(c.EndUtc.HasValue && c.StartUtc is null))
            .WithMessage("EndUtc requires StartUtc.");
        RuleFor(command => command)
            .Must(c => !(c.StartUtc.HasValue && c.EndUtc.HasValue) || c.EndUtc.Value >= c.StartUtc.Value)
            .WithMessage("EndUtc must not be before StartUtc.");

        RuleFor(c => c.RecurrenceInterval).GreaterThanOrEqualTo(1);
        RuleFor(c => c.RecurrenceCount).GreaterThan(0).When(c => c.RecurrenceCount.HasValue);
        RuleFor(c => c)
            .Must(c => c.RecurrenceFrequency == RecurrenceFrequency.None || c.StartUtc.HasValue)
            .WithMessage("A recurring item needs a start date.");
        RuleFor(c => c)
            .Must(c => !(c.RecurrenceEndUtc.HasValue && c.StartUtc.HasValue) || c.RecurrenceEndUtc.Value >= c.StartUtc.Value)
            .WithMessage("RecurrenceEndUtc must not be before StartUtc.");
    }
}
