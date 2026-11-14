using System.Text.RegularExpressions;
using FluentValidation;
using ProjectK.Common.Models.Dtos.UsersModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout.Save;

public sealed partial class SaveTileLayoutCommandValidator : AbstractValidator<SaveTileLayoutCommand>
{
    private const int MaxTileCount = 40;
    private const int MaxTileKeyLength = 64;

    public SaveTileLayoutCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.BoardKey)
            .Must(TileBoardKeys.All.Contains)
            .WithMessage("Unknown board key.");

        RuleFor(command => command.TileKeys)
            .Must(keys => (keys?.Count ?? 0) <= MaxTileCount)
                .WithMessage($"A layout cannot contain more than {MaxTileCount} tiles.")
            .Must(AllKeysWellFormed)
                .WithMessage("Tile keys must be non-empty, at most 64 lowercase alphanumeric/hyphen characters.")
            .Must(AllKeysUnique)
                .WithMessage("Tile keys must be unique.");

        RuleFor(command => command.HiddenTileKeys)
            .Must(keys => (keys?.Count ?? 0) <= MaxTileCount)
                .WithMessage($"A layout cannot hide more than {MaxTileCount} tiles.")
            .Must(AllKeysWellFormed)
                .WithMessage("Hidden tile keys must be non-empty, at most 64 lowercase alphanumeric/hyphen characters.")
            .Must(AllKeysUnique)
                .WithMessage("Hidden tile keys must be unique.");
    }

    private static bool AllKeysWellFormed(IReadOnlyList<string>? keys)
        => keys is null || keys.All(key =>
            !string.IsNullOrWhiteSpace(key) && key.Length <= MaxTileKeyLength && TileKeyPattern().IsMatch(key));

    private static bool AllKeysUnique(IReadOnlyList<string>? keys)
        => keys is null || keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex TileKeyPattern();
}
