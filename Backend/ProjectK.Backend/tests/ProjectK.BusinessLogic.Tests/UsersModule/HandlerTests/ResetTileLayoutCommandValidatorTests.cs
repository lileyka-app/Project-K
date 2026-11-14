using FluentValidation.TestHelper;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;
using ProjectK.Common.Models.Dtos.UsersModule;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

public class ResetTileLayoutCommandValidatorTests
{
    private readonly ResetTileLayoutCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_ForKnownBoard()
    {
        var command = new ResetTileLayoutCommand(Guid.NewGuid(), TileBoardKeys.MemberCard);
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_ForUnknownBoard()
    {
        var command = new ResetTileLayoutCommand(Guid.NewGuid(), "nope");
        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(x => x.BoardKey)
            .WithErrorMessage("Unknown board key.");
    }
}
