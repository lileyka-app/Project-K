using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.SavedAccount;

/// <summary>
/// The account switcher's sessions: kept per account, never more than a few, and never dropped
/// without being ended, so no session outlives the cookie that could reach it.
/// </summary>
public class SavedAccountHandlerTests
{
    private readonly Mock<IRefreshTokenStore> _store = new();
    private readonly Dictionary<string, UserRefreshToken> _sessions = new(StringComparer.Ordinal);
    private readonly SavedSessionList _saved;

    public SavedAccountHandlerTests()
    {
        _store.Setup(s => s.FindActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string token, CancellationToken _) => _sessions.GetValueOrDefault(token));
        _store.Setup(s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string token, CancellationToken _) => _sessions.Remove(token));
        _saved = new SavedSessionList(_store.Object);
    }

    [Fact]
    public async Task Add_ShouldKeepThePreviousSession_AndEndTheNewAccountsOldOne()
    {
        var work = Guid.NewGuid();
        var admin = Guid.NewGuid();
        GivenSession("admin-kept-earlier", admin);
        GivenSession("work-in-use", work);

        var result = await new AddSavedAccountCommandHandler(_saved).Handle(
            new AddSavedAccountCommand(["admin-kept-earlier"], ["work-in-use"], admin),
            CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(["work-in-use"], result.Data!.Select(s => s.Token));
        // Signing in to the admin again makes its kept session redundant; it is ended, not orphaned.
        _store.Verify(s => s.RevokeAsync("admin-kept-earlier", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tidy_ShouldKeepTheNewestSessionPerAccount_AndCapTheList()
    {
        var tokens = new List<string>();
        for (var i = 0; i < SavedSessionList.MaxAccounts + 2; i++)
        {
            tokens.Add(GivenSession($"account-{i}", Guid.NewGuid()));
        }
        var repeat = Guid.NewGuid();
        tokens.Insert(0, GivenSession("repeat-old", repeat));
        tokens.Add(GivenSession("repeat-new", repeat));

        var kept = await _saved.TidyAsync(tokens, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SavedSessionList.MaxAccounts, kept.Count);
        Assert.Equal("repeat-new", kept[^1].Token);
        Assert.Equal(kept.Count, kept.Select(s => s.UserKey).Distinct().Count());
        // Everything left out is ended.
        _store.Verify(s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(tokens.Count - kept.Count));
    }

    [Fact]
    public async Task Switch_ShouldSpendTheKeptSession_AndKeepTheOneThatWasInUse()
    {
        var work = new AppUser { Id = Guid.NewGuid(), Email = "work@test" };
        var admin = new AppUser { Id = Guid.NewGuid(), Email = "admin@test" };
        GivenSession("admin-kept", admin.Id);
        GivenSession("work-in-use", work.Id);
        var handler = Handler(admin, out var factory);

        var result = await handler.Handle(
            new SwitchSavedAccountCommand(admin.Id, ["admin-kept"], ["work-in-use"]),
            CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(admin.Id, result.Data!.Login.UserKey);
        Assert.Equal(["work-in-use"], result.Data.Saved.Select(s => s.Token));
        _store.Verify(s => s.RevokeAsync("admin-kept", It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.RevokeAsync("work-in-use", It.IsAny<CancellationToken>()), Times.Never);
        factory.Verify(f => f.CreateAsync(admin, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Switch_ShouldRefuse_AnAccountTheBrowserHoldsNoSessionFor()
    {
        var admin = new AppUser { Id = Guid.NewGuid(), Email = "admin@test" };
        GivenSession("work-in-use", Guid.NewGuid());
        var handler = Handler(admin, out var factory);

        var result = await handler.Handle(
            new SwitchSavedAccountCommand(admin.Id, [], ["work-in-use"]),
            CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        factory.Verify(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Switch_ShouldRefuse_ASuspendedAccount()
    {
        var admin = new AppUser { Id = Guid.NewGuid(), Email = "admin@test", OnboardingStatus = OnboardingStatus.Suspended };
        GivenSession("admin-kept", admin.Id);
        var handler = Handler(admin, out var factory);

        var result = await handler.Handle(
            new SwitchSavedAccountCommand(admin.Id, ["admin-kept"], []),
            CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        factory.Verify(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private SwitchSavedAccountCommandHandler Handler(AppUser target, out Mock<ILoginResponseFactory> factory)
    {
        var userManager = new Mock<UserManager<AppUser>>(
            new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager.Setup(m => m.FindByIdAsync(target.Id.ToString())).ReturnsAsync(target);

        factory = new Mock<ILoginResponseFactory>();
        factory.Setup(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) => Logins.Empty with
            {
                UserKey = user.Id,
                Email = user.Email!,
                Tokens = new JwtResponse
                {
                    AccessToken = "access",
                    RefreshToken = new ProjectK.Common.Dtos.AuthModule.RefreshToken { Token = "issued", Expires = DateTime.UtcNow.AddDays(7) }
                }
            });

        return new SwitchSavedAccountCommandHandler(_saved, _store.Object, userManager.Object, factory.Object);
    }

    private string GivenSession(string token, Guid userId)
    {
        _sessions[token] = new UserRefreshToken { Token = token, UserId = userId, ExpiresAtUtc = DateTime.UtcNow.AddDays(7) };
        return token;
    }
}
