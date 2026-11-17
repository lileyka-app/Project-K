using FluentValidation.TestHelper;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.TileLayout;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.TileLayout;

public class SaveTileLayoutCommandValidatorTests
{
    private readonly SaveTileLayoutCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_ForValidLayout()
    {
        var command = new SaveTileLayoutCommand(Guid.NewGuid(), TileBoardNames.MemberCard, new[] { "profile", "skills" }, Array.Empty<string>(), 1);
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_ForUnknownBoard()
    {
        var command = new SaveTileLayoutCommand(Guid.NewGuid(), "not-a-board", new[] { "profile" }, Array.Empty<string>(), 1);
        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.BoardKey)
            .WithErrorMessage("Unknown board key.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenTooManyTiles()
    {
        var tiles = Enumerable.Range(0, 41).Select(i => $"tile-{i}").ToArray();
        var command = new SaveTileLayoutCommand(Guid.NewGuid(), TileBoardNames.MemberCard, tiles, Array.Empty<string>(), 1);
        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.TileKeys)
            .WithErrorMessage("A layout cannot contain more than 40 tiles.");
    }

    [Theory]
    [InlineData("Profile")]     // uppercase not allowed
    [InlineData("has space")]   // space not allowed
    [InlineData("under_score")] // underscore not allowed
    public void Validate_ShouldFail_ForMalformedTileKey(string badKey)
    {
        var command = new SaveTileLayoutCommand(Guid.NewGuid(), TileBoardNames.MemberCard, new[] { badKey }, Array.Empty<string>(), 1);
        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.TileKeys)
            .WithErrorMessage("Tile keys must be non-empty, at most 64 lowercase alphanumeric/hyphen characters.");
    }

    [Fact]
    public void Validate_ShouldFail_ForDuplicateTileKeys()
    {
        var command = new SaveTileLayoutCommand(Guid.NewGuid(), TileBoardNames.MemberCard, new[] { "profile", "profile" }, Array.Empty<string>(), 1);
        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.TileKeys)
            .WithErrorMessage("Tile keys must be unique.");
    }
}
