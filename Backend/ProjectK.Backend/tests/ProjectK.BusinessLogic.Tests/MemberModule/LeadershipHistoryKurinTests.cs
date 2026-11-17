using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>
/// A card carries offices from every kurin the person stands in; each says which kurin it is in,
/// so the card shows it there only and names it by that kurin's own role names.
/// </summary>
public class LeadershipHistoryKurinTests
{
    private static LeadershipHistory HeldIn(Leadership office) => new()
    {
        LeadershipHistoryKey = Guid.NewGuid(),
        LeadershipKey = office.LeadershipKey,
        Leadership = office,
        Role = LeadershipRole.Pysar,
        StartDate = new DateOnly(2025, 9, 1)
    };

    [Fact]
    public void AKurinOffice_SaysItsOwnKurin()
    {
        var kurinKey = Guid.NewGuid();
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Kurin, KurinKey = kurinKey };

        HeldIn(office).ToDto(new Dictionary<Guid, GroupSummary>()).KurinKey.Should().Be(kurinKey);
    }

    [Fact]
    public void AGroupOffice_SaysTheKurinItsGroupIsIn()
    {
        var kurinKey = Guid.NewGuid();
        var group = new GroupSummary(Guid.NewGuid(), kurinKey, "Соколи");
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Group, GroupKey = group.GroupKey };

        var dto = HeldIn(office).ToDto(new Dictionary<Guid, GroupSummary> { [group.GroupKey] = group });

        dto.KurinKey.Should().Be(kurinKey);
        dto.GroupName.Should().Be("Соколи");
    }
}
