using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Get;
using ProjectK.BusinessLogic.Modules.UsersModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

public class GetAllUsersQueryHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAppUserRepository> _appUserRepositoryMock = new();
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IMembershipDirectory> _membershipsMock = new();
    private readonly GetAllUsersQueryHandler _handler;

    public GetAllUsersQueryHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock.Setup(u => u.Kurins).Returns(_kurinRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_appUserRepositoryMock.Object);
        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Kurin>());
        StandingNowhere();

        _handler = new GetAllUsersQueryHandler(
            _userManagerMock.Object,
            _unitOfWorkMock.Object,
            _membershipsMock.Object);
    }

    /// <summary>Nobody has a current membership, which is what an account alone amounts to.</summary>
    private void StandingNowhere()
        => _membershipsMock
            .Setup(x => x.GetCurrentForAccountsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<MembershipRecord>>());

    private void Standing(Guid userKey, Guid kurinKey, int kurinNumber)
        => _membershipsMock
            .Setup(x => x.GetCurrentForAccountsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<MembershipRecord>>
            {
                [userKey] =
                [
                    new MembershipRecord(
                        Guid.NewGuid(), kurinKey, kurinNumber, KurinBranch.UPYu, null,
                        null, null, MembershipKind.Youth, DateTime.UtcNow, null)
                ]
            });

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUsersExist()
    {
        // Arrange
        var kurinKey1 = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = userId,
                Email = "user1@example.com",
                FirstName = "John",
                LastName = "Doe"
            }
        }.AsQueryable();

        Standing(userId, kurinKey1, 101);

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var userDto = result.Data.First();
        Assert.Equal(101, userDto.KurinNumber);
    }

    [Fact]
    public async Task Handle_ShouldNameTheKurinsUlad_SoTheSameNumberInAnotherUladIsAnotherKurin()
    {
        var userId = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        SetupUserManagerUsers(new List<AppUser> { new() { Id = userId, Email = "orden@example.com", FirstName = "Остап", LastName = "Гнатюк" } }.AsQueryable());
        _membershipsMock
            .Setup(x => x.GetCurrentForAccountsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyCollection<MembershipRecord>>
            {
                [userId] =
                [
                    new MembershipRecord(
                        Guid.NewGuid(), kurinKey, 5, KurinBranch.USP, "Орден Хрестоносців",
                        null, null, MembershipKind.Staff, DateTime.UtcNow, null,
                        [new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)])
                ]
            });

        var userDto = (await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None)).Data!.Single();

        Assert.Equal(kurinKey, userDto.KurinKey);
        Assert.Equal(KurinBranch.USP, userDto.KurinBranch);
        Assert.Equal("Орден Хрестоносців", userDto.KurinNamedAfter);
        Assert.Equal([new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)], userDto.KurinBranches);
    }

    [Fact]
    public async Task Handle_ShouldGiveAYouthKurinItsOneUlad_WhenTheRecordCarriesNoBranches()
    {
        var userId = Guid.NewGuid();
        SetupUserManagerUsers(new List<AppUser> { new() { Id = userId, Email = "kurin5@example.com", FirstName = "Соломія", LastName = "Дмитрук" } }.AsQueryable());
        Standing(userId, Guid.NewGuid(), 5);

        var userDto = (await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None)).Data!.Single();

        Assert.Equal(KurinBranch.UPYu, userDto.KurinBranch);
        Assert.Equal([new KurinNumbering(KurinBranch.UPYu, 5)], userDto.KurinBranches);
    }

    [Fact]
    public async Task Handle_ShouldNameTheKurinsKrai_SinceTwoKraiCanEachHaveAKurin5()
    {
        var userId = Guid.NewGuid();
        var kurin = new Kurin(5) { KraiCode = "cz" };
        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Kurin> { kurin });
        SetupUserManagerUsers(new List<AppUser> { new() { Id = userId, Email = "cz5@example.com", FirstName = "Ян", LastName = "Новак" } }.AsQueryable());
        Standing(userId, kurin.KurinKey, 5);

        var userDto = (await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None)).Data!.Single();

        Assert.Equal("cz", userDto.KurinKraiCode);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithEmptyList_WhenNoUsersExist()
    {
        // Arrange
        var users = new List<AppUser>().AsQueryable();
        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);

        _userManagerMock.Verify(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldHandleUsersWithMultipleRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = userId,
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
            }
        }.AsQueryable();

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.Is<AppUser>(u => u.Id == userId), SystemRole.Admin))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Single(result.Data);
        Assert.Equal("Admin", result.Data.First().Role); // Should return first role
    }

    [Fact]
    public async Task Handle_ShouldHandleUsersWithNoRoles()
    {
        // Arrange
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = Guid.NewGuid(),
                Email = "noroles@example.com",
                FirstName = "No",
                LastName = "Roles",
            }
        }.AsQueryable();

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Single(result.Data);
        Assert.Equal("Member", result.Data.First().Role);
    }

    [Fact]
    public async Task Handle_ShouldReportNoKurin_WhenAccountHasNoCurrentMembership()
    {
        // Arrange
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = Guid.NewGuid(),
                Email = "nullkurin@example.com",
                FirstName = "Null",
                LastName = "Kurin",
                // Set, and still ignored: an account's own kurin field is a stale snapshot.
            }
        }.AsQueryable();

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Single(result.Data);
        Assert.Null(result.Data.First().KurinKey);
    }

    [Fact]
    public async Task Handle_ShouldHandleDifferentUserRoles()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user3Id = Guid.NewGuid();

        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = user1Id,
                Email = "admin@example.com",
                FirstName = "Admin",
                LastName = "User",
            },
            new AppUser
            {
                Id = user2Id,
                Email = "manager@example.com",
                FirstName = "Manager",
                LastName = "User",
            },
            new AppUser
            {
                Id = user3Id,
                Email = "regular@example.com",
                FirstName = "Regular",
                LastName = "User",
            }
        }.AsQueryable();

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.Is<AppUser>(u => u.Id == user1Id), SystemRole.Admin))
            .ReturnsAsync(true);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.Is<AppUser>(u => u.Id == user2Id), SystemRole.Admin))
            .ReturnsAsync(false);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.Is<AppUser>(u => u.Id == user3Id), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(3, result.Data.Count());

        var userList = result.Data.ToList();
        Assert.Equal("Admin", userList[0].Role);
        Assert.Equal("Member", userList[1].Role);
        Assert.Equal("Member", userList[2].Role);
    }

    [Fact]
    public async Task Handle_ShouldMapAllUserProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = userId,
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User"
            }
        }.AsQueryable();

        Standing(userId, kurinKey, 7);

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Single(result.Data);

        var userDto = result.Data.First();
        Assert.Equal(userId, userDto.UserId);
        Assert.Equal(kurinKey, userDto.KurinKey);
        Assert.Equal("test@example.com", userDto.Email);
        Assert.Equal("Test", userDto.FirstName);
        Assert.Equal("User", userDto.LastName);
        Assert.Equal("Member", userDto.Role);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessEvenWhenResultIsNull()
    {
        // Arrange
        var users = new List<AppUser>().AsQueryable();
        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldCheckTheSystemRoleOfEachUser()
    {
        // Arrange
        var users = new List<AppUser>
        {
            new AppUser { Id = Guid.NewGuid(), Email = "user1@test.com", FirstName = "User1", LastName = "Test" },
            new AppUser { Id = Guid.NewGuid(), Email = "user2@test.com", FirstName = "User2", LastName = "Test" },
            new AppUser { Id = Guid.NewGuid(), Email = "user3@test.com", FirstName = "User3", LastName = "Test" }
        }.AsQueryable();

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users);
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _userManagerMock.Verify(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ShouldHandleLargeNumberOfUsers()
    {
        // Arrange
        var users = new List<AppUser>();
        for (int i = 0; i < 100; i++)
        {
            users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@example.com",
                FirstName = $"User{i}",
                LastName = "Test",
            });
        }

        var query = new GetAllUsersQuery();

        SetupUserManagerUsers(users.AsQueryable());
        _userManagerMock.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(100, result.Data.Count());
        _userManagerMock.Verify(x => x.IsInRoleAsync(It.IsAny<AppUser>(), SystemRole.Admin), Times.Exactly(100));
    }

    [Fact]
    public void Constructor_ShouldInitializeUserManagerCorrectly()
    {
        // Arrange & Act
        var handler = new GetAllUsersQueryHandler(
            _userManagerMock.Object,
            _unitOfWorkMock.Object,
            _membershipsMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    private void SetupUserManagerUsers(IQueryable<AppUser> users)
    {
        _appUserRepositoryMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users.ToList());
    }
}
