using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Groups;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>Which гуртки the dashboard offers: the person's own and the ones they lead, nothing else of the kurin.</summary>
public class GetMyGroupsQueryTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();

    private readonly Group _own = new("Соколи", Kurin) { SilhouetteBlobName = "sokoly.png" };
    private readonly Group _led = new("Кельти", Kurin);
    private readonly Group _other = new("Лиси", Kurin);

    private GetMyGroupsQueryHandler Handler(Guid? ownGroup, params Guid[] led) => Handler(ownGroup, holdsOffice: true, led);

    private GetMyGroupsQueryHandler Handler(Guid? ownGroup, bool holdsOffice, params Guid[] led)
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(User);
        user.SetupGet(u => u.KurinKey).Returns(Kurin);

        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetCurrentForAccountAsync(User, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MembershipRecord(Guid.NewGuid(), Kurin, 51, KurinBranch.UPYu, null, ownGroup, null, MembershipKind.Youth, DateTime.UtcNow, null)
        ]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(Person, User, Kurin, ownGroup, "Оксана", "Паливода", "o@x", null));
        members.Setup(m => m.GetLevelsAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var scopes = new Mock<IResourceScopeRepository>();
        scopes.Setup(s => s.GetLedGroupKeysAsync(User, Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(led);

        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([_other, _led, _own]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Groups).Returns(groups.Object);

        var links = new Mock<IBlobReadLinks>();
        links.Setup(l => l.For(It.IsAny<string?>())).Returns((string? name) => name is null ? null : $"https://blob/{name}");

        var offices = new Mock<IOfficeDirectory>();
        offices.Setup(o => o.GetForAccountInKurinAsync(User, Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdsOffice ? [new MemberOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy)] : []);

        return new GetMyGroupsQueryHandler(
            new CurrentMember(user.Object, members.Object, memberships.Object), offices.Object, UnitOfWorks.WithScopes(scopes.Object, unitOfWork), links.Object, user.Object);
    }

    [Fact]
    public async Task OwnGurtokComesFirst_ThenTheLedOnes_WithTheirSilhouettes()
    {
        var result = await Handler(_own.GroupKey, _led.GroupKey).Handle(new GetMyGroupsQuery(), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Select(g => g.Name).Should().Equal("Соколи", "Кельти");
        result.Data![0].IsOwn.Should().BeTrue();
        result.Data![0].SilhouetteUrl.Should().Be("https://blob/sokoly.png");
        result.Data![1].IsLed.Should().BeTrue();
        result.Data![1].SilhouetteUrl.Should().BeNull();
        result.Data!.Should().OnlyContain(g => g.Kurin.KurinNumber == 51 && g.Kurin.IsCurrent);
    }

    [Fact]
    public async Task AVporiadnykOutsideAnyGurtok_SeesOnlyWhatTheyLead()
    {
        var result = await Handler(null, _led.GroupKey).Handle(new GetMyGroupsQuery(), CancellationToken.None);

        result.Data!.Select(g => g.Name).Should().Equal("Кельти");
    }

    // The scope reader counts a member's own гурток as led; without an office that is not leading it.
    [Fact]
    public async Task AYouthWithoutAnOffice_IsNotAVporiadnykOfTheirOwnGurtok()
    {
        var result = await Handler(_own.GroupKey, holdsOffice: false, _own.GroupKey).Handle(new GetMyGroupsQuery(), CancellationToken.None);

        result.Data!.Should().ContainSingle(g => g.IsOwn && !g.IsLed);
    }

    [Fact]
    public async Task NoGurtokAtAll_IsAnEmptyList()
    {
        var result = await Handler(null).Handle(new GetMyGroupsQuery(), CancellationToken.None);

        result.Data.Should().BeEmpty();
    }
}
