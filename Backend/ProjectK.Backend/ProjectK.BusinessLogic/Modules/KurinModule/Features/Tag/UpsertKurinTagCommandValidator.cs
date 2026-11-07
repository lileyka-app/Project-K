using FluentValidation;
using ProjectK.Common.Models.Tags;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

public sealed class UpsertKurinTagCommandValidator : AbstractValidator<UpsertKurinTagCommand>
{
    public UpsertKurinTagCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= KurinTagRules.MaxNameLength);
        RuleFor(c => c.ColorHex)
            .Must(KurinTagRules.IsPaletteColor)
            .WithErrorCode(KurinTagErrors.TagColorNotInPalette);
    }
}
