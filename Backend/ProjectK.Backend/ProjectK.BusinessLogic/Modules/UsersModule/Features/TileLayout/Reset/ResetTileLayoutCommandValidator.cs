using FluentValidation;
using ProjectK.Common.Models.Dtos.UsersModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout.Reset;

public sealed class ResetTileLayoutCommandValidator : AbstractValidator<ResetTileLayoutCommand>
{
    public ResetTileLayoutCommandValidator()
    {
        RuleFor(command => command.BoardKey)
            .Must(TileBoardKeys.All.Contains)
            .WithMessage("Unknown board key.");
    }
}
