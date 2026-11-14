using FluentAssertions;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class MemberSummaryNamesTests
{
    private static MemberSummary Person(Guid memberKey, Guid? userKey, string first, string last)
        => new(memberKey, userKey, Guid.NewGuid(), null, first, last, "person@example.com", null);

    [Fact]
    public void ByMember_NamesEachPersonOnce_ByTheFirstRowRead()
    {
        var key = Guid.NewGuid();
        var other = Guid.NewGuid();

        var names = MemberSummaryNames.ByMember(
        [
            Person(key, null, "Оксана", "Петренко"),
            Person(key, null, "Оксана", "Іваненко"),
            Person(other, Guid.NewGuid(), "", "Коваль")
        ]);

        names.Should().HaveCount(2);
        names[key].Should().Be("Оксана Петренко");
        names[other].Should().Be("Коваль");
    }

    [Fact]
    public void ByAccount_LeavesOutPeopleWithoutAnAccount_AndKeepsTheFirstRowPerAccount()
    {
        var account = Guid.NewGuid();

        var names = MemberSummaryNames.ByAccount(
        [
            Person(Guid.NewGuid(), null, "Без", "Акаунта"),
            Person(Guid.NewGuid(), account, "Оксана", "Петренко"),
            Person(Guid.NewGuid(), account, "Інша", "Людина")
        ]);

        names.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, string>(account, "Оксана Петренко"));
    }

    [Fact]
    public void ByAccount_KeepsAnEmptyAccountKey_WhichOnlyTheAgendaLeavesOut()
    {
        var people = new[] { Person(Guid.NewGuid(), Guid.Empty, "Оксана", "Петренко") };

        MemberSummaryNames.ByAccount(people).Should().ContainKey(Guid.Empty);
        AgendaCreatorNames.FromMembers(people).Should().BeEmpty();
    }
}
