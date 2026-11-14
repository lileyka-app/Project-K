using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Infrastructure.Seeding;

namespace ProjectK.Infrastructure.Tests.Seeding;

/// <summary>
/// The seeded administrator is the one account whose password is in the repository, so the rule is
/// that it may only ever appear in a database that has no administrator at all.
/// </summary>
public class SeededAdministratorTests
{
    private const string SeededEmail = "admin@projectk.com";

    [Fact]
    public async Task NoAdministratorAtAll_CreatesTheSeededOne()
    {
        var userManager = CreateUserManager();

        await DataSeeder.EnsureSeededAdministratorAsync(EmptyServices(), userManager.Object);

        userManager.Verify(
            m => m.CreateAsync(It.Is<AppUser>(u => u.Email == SeededEmail), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task AdministratorUnderAnotherAddress_LeavesTheSeededOneUncreated()
    {
        var userManager = CreateUserManager(Administrator("zvyazkovyi@kurin.example"));

        await DataSeeder.EnsureSeededAdministratorAsync(EmptyServices(), userManager.Object);

        userManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The account that was moved to a real address is the same account, and what is left at the old
    /// one is nothing — which is precisely when the seeder used to make a second administrator.
    /// </summary>
    [Fact]
    public async Task SeededAdministratorRenamedToARealAddress_IsNotRecreated()
    {
        var renamed = Administrator("rostyslav@example.org");
        var userManager = CreateUserManager(renamed);
        userManager.Setup(m => m.FindByEmailAsync(SeededEmail)).ReturnsAsync((AppUser?)null);

        await DataSeeder.EnsureSeededAdministratorAsync(EmptyServices(), userManager.Object);

        userManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnlyTheSeededAdministratorExists_StillSeedsAndFindsItInPlace()
    {
        var seeded = Administrator(SeededEmail);
        var userManager = CreateUserManager(seeded);
        userManager.Setup(m => m.FindByEmailAsync(SeededEmail)).ReturnsAsync(seeded);

        await DataSeeder.EnsureSeededAdministratorAsync(EmptyServices(), userManager.Object);

        userManager.Verify(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        userManager.Verify(m => m.FindByEmailAsync(SeededEmail), Times.Once);
    }

    [Theory]
    [InlineData("ADMIN@PROJECTK.COM")]
    [InlineData("Admin@ProjectK.com")]
    public async Task TheSeededAddressIsRecognisedWhateverItsCase(string email)
    {
        var userManager = CreateUserManager(Administrator(email));

        var configured = await DataSeeder.HasConfiguredAdministratorAsync(userManager.Object);

        Assert.False(configured);
    }

    private static AppUser Administrator(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email
    };

    private static Mock<UserManager<AppUser>> CreateUserManager(params AppUser[] administrators)
    {
        var userManager = new Mock<UserManager<AppUser>>(
            new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);

        userManager
            .Setup(m => m.GetUsersInRoleAsync(SystemRoleNames.Admin))
            .ReturnsAsync(administrators.ToList<AppUser>());
        userManager
            .Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((AppUser?)null);
        userManager
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        return userManager;
    }

    private static IServiceProvider EmptyServices() => new ServiceCollection().BuildServiceProvider();
}
