using FluentValidation.TestHelper;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.TileLayout;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.TileLayout;

public class ResetTileLayoutCommandValidatorTests
{
    private readonly ResetTileLayoutCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_ForKnownBoard()
    {
        var command = new ResetTileLayoutCommand(Guid.NewGuid(), TileBoardNames.MemberCard);
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
