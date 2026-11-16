using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.BusinessLogic.Modules.AuthModule;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.AuthModule;

/// <summary>
/// The "my account" lookup and its one answer for a token that names a missing account.
/// </summary>
public class SignedInAccountTests
{
    private readonly Mock<UserManager<AppUser>> _userManager = new(
        new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

    [Fact]
    public async Task FindSignedInAsync_ShouldLookTheAccountUpByItsKey()
    {
        var account = new AppUser { Id = Guid.NewGuid() };
        _userManager.Setup(m => m.FindByIdAsync(account.Id.ToString())).ReturnsAsync(account);

        var found = await _userManager.Object.FindSignedInAsync(account.Id);

        found.Should().BeSameAs(account);
    }

    [Fact]
    public async Task FindSignedInAsync_ShouldAnswerNull_WhenTheAccountIsGone()
    {
        var found = await _userManager.Object.FindSignedInAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public void AccountMissing_ShouldAnswerAsAnExpiredSignIn()
    {
        var result = AuthErrors.AccountMissing<int>();

        result.Type.Should().Be(ResultType.Unauthorized);
        result.ErrorCode.Should().Be("Unauthorized");
        result.ErrorMessage.Should().Be("User not found or unauthorized.");
    }
}
