using FluentAssertions;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class MentorOfficeRulesTests
{
    private static readonly MemberOffice Zvyazkovyi = new(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly MemberOffice Pysar = new(LeadershipType.Group, LeadershipRole.Pysar);

    private static MemberLookupRow Person(MemberOffice? office, Guid? memberKey = null) =>
        new(memberKey ?? Guid.NewGuid(), Guid.NewGuid(), "Ім'я", null, "Прізвище", null, office);

    [Fact]
    public void WithAssignment_ShouldAddTheVykhovnyk_OnlyForAnAccountThatRunsAGroup()
    {
        MentorOfficeRules.WithAssignment([Pysar], runsAGroup: true).Should().Equal(Pysar, MentorOfficeRules.Office);
        MentorOfficeRules.WithAssignment([Pysar], runsAGroup: false).Should().Equal(Pysar);
    }

    [Fact]
    public void WithAssignment_ShouldNotCountTheVykhovnykTwice_ForSomeoneSeatedAsOne()
    {
        MentorOfficeRules.WithAssignment([MentorOfficeRules.Office], runsAGroup: true)
            .Should().Equal(MentorOfficeRules.Office);
    }

    [Fact]
    public void KvWithMentors_ShouldListTheSeatedFirst_ThenTheUnseatedMentorsAsVykhovnyky()
    {
        var seated = Person(Zvyazkovyi);
        var alsoMentor = Person(null, seated.MemberKey);
        var mentor = Person(null);

        var kv = MentorOfficeRules.KvWithMentors([seated], [alsoMentor, mentor, mentor]);

        kv.Should().HaveCount(2);
        kv[0].Should().Be(seated);
        kv[1].MemberKey.Should().Be(mentor.MemberKey);
        kv[1].Office.Should().Be(MentorOfficeRules.Office);
    }
}
