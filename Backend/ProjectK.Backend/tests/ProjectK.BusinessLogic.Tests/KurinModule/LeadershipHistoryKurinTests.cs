using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>
/// A card carries offices from every kurin the person stands in; each says which kurin it is in,
/// so the card shows it there only and names it by that kurin's own role names.
/// </summary>
public class LeadershipHistoryKurinTests
{
    private static readonly Guid Person = Guid.NewGuid();

    private static LeadershipHistory HeldIn(Leadership office) => new()
    {
        LeadershipHistoryKey = Guid.NewGuid(),
        MemberKey = Person,
        LeadershipKey = office.LeadershipKey,
        Leadership = office,
        Role = LeadershipRole.Pysar,
        StartDate = new DateOnly(2025, 9, 1)
    };

    private static OfficeDirectory DirectoryHolding(LeadershipHistory history, params Group[] groups)
    {
        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(l => l.GetHistoriesForMemberAsync(Person, It.IsAny<CancellationToken>())).ReturnsAsync([history]);
        var groupRepository = new Mock<IGroupRepository>();
        groupRepository
            .Setup(g => g.GetByKeysIncludingDissolvedAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        unitOfWork.SetupGet(u => u.Groups).Returns(groupRepository.Object);
        return new OfficeDirectory(unitOfWork.Object);
    }

    [Fact]
    public async Task AKurinOffice_SaysItsOwnKurin()
    {
        var kurinKey = Guid.NewGuid();
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Kurin, KurinKey = kurinKey };

        var held = Assert.Single(await DirectoryHolding(HeldIn(office)).GetHeldByMemberAsync(Person));

        held.KurinKey.Should().Be(kurinKey);
    }

    [Fact]
    public async Task AGroupOffice_SaysTheKurinItsGroupIsIn()
    {
        var kurinKey = Guid.NewGuid();
        var group = new Group("Соколи", kurinKey);
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Group, GroupKey = group.GroupKey };

        var held = Assert.Single(await DirectoryHolding(HeldIn(office), group).GetHeldByMemberAsync(Person));

        held.KurinKey.Should().Be(kurinKey);
        held.GroupName.Should().Be("Соколи");
    }
}
