using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

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

        HeldIn(office).ToDto().KurinKey.Should().Be(kurinKey);
    }

    [Fact]
    public void AGroupOffice_SaysTheKurinItsGroupIsIn()
    {
        var kurinKey = Guid.NewGuid();
        var group = new Group("Соколи", kurinKey) { GroupKey = Guid.NewGuid() };
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Group, GroupKey = group.GroupKey, Group = group };

        HeldIn(office).ToDto().KurinKey.Should().Be(kurinKey);
    }
}
