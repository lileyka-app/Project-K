using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.ReportTests;

/// <summary>
/// The pieces the звіт куреня is put together from: the names it gives a person, a role and a point
/// of a проба, and its header.
/// </summary>
public sealed class KurinReportPartsTests
{
    private static KurinReportSourceMember Person(string first, string? middle, string last, params KurinReportPlastLevel[] levels) => new(
        Guid.NewGuid(), null, first, middle, last, "a@example.com", "+380500000000", new DateOnly(2010, 1, 1),
        null, null, null, PlastLevel.Entry, levels, [], [], []);

    [Fact]
    public void Initials_TakeTheFirstLetterOfEveryNameGiven()
    {
        KurinReportRosterRules.Initials(Person("іван", "петрович", "Коваль")).Should().Be("ІПК");
        KurinReportRosterRules.Initials(Person("Іван", null, "Коваль")).Should().Be("ІК");
    }

    [Fact]
    public void LatestLevelOf_PrefersTheNewestRecordedOverTheStoredField()
    {
        var member = Person("Іван", null, "Коваль",
            new KurinReportPlastLevel(PlastLevel.Prykhylnyk, new DateOnly(2021, 1, 1)),
            new KurinReportPlastLevel(PlastLevel.Uchasnyk, new DateOnly(2022, 1, 1)));

        KurinReportRosterRules.LatestLevelOf(member).Should().Be(PlastLevel.Uchasnyk);
        KurinReportRosterRules.LatestLevelOf(Person("Іван", null, "Коваль")).Should().Be(PlastLevel.Entry);
    }

    [Fact]
    public void LeadershipScopeName_NamesAGroupRoleByItsGurtok_AndAKurinRoleByNothing()
    {
        var groupKey = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [groupKey] = "Соколи" };
        var groupRole = new KurinReportSourceOffice(LeadershipType.Group, null, groupKey, null, null, LeadershipRole.Hurtkoviy, new DateOnly(2025, 9, 1), null);
        var kurinRole = groupRole with { Type = LeadershipType.Kurin, GroupKey = null };

        KurinReportRosterRules.LeadershipScopeName(groupRole, names).Should().Be("Соколи");
        KurinReportRosterRules.LeadershipScopeName(kurinRole, names).Should().BeNull();
        KurinReportRosterRules.LeadershipScopeName(groupRole with { Name = "Свій провід" }, names).Should().Be("Свій провід");
    }

    [Fact]
    public void ProbePointLabel_GivesTheSectionCodeAndPlace_OrTheIdWhenUnknown()
    {
        var probe = new GroupedProbeDto("p1", "Перша проба", 2, 1,
        [
            new ProbeSection("s1", "Б", "Section", [new ProbePoint("a", ""), new ProbePoint("b", "Вузли")])
        ]);

        KurinReportProgressNames.ProbePointLabel("b", probe).Should().Be("точка Б2 - Вузли");
        KurinReportProgressNames.ProbePointLabel("a", probe).Should().Be("точка Б1");
        KurinReportProgressNames.ProbePointLabel("z", probe).Should().Be("z");
        KurinReportProgressNames.ProbeTitle("p1", null).Should().Be("p1");
        KurinReportProgressNames.ProbeTitle("p1", probe).Should().Be("Перша проба");
    }

    [Fact]
    public void Header_NamesTheCurrentUser_AndSaysUnknownForAMissingVersion()
    {
        var userKey = Guid.NewGuid();
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(userKey);
        var users = new Dictionary<Guid, AppUser>
        {
            [userKey] = new() { Id = userKey, FirstName = "Олена", LastName = "Пчілка", Email = "o@example.com" }
        };

        var header = new KurinReportHeaderReader(user.Object, new ConfigurationBuilder().Build()).Read(users);

        header.GeneratedByName.Should().Be("Олена Пчілка");
        header.GeneratedByEmail.Should().Be("o@example.com");
        header.BackendVersion.Should().Be("unknown");

        var stranger = new KurinReportHeaderReader(Mock.Of<ICurrentUserContext>(), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ReleaseInfo:Version"] = "v1.3" })
            .Build()).Read(users);
        stranger.GeneratedByName.Should().Be("Unknown user");
        stranger.GeneratedByEmail.Should().BeNull();
        stranger.BackendVersion.Should().Be("v1.3");
    }
}
