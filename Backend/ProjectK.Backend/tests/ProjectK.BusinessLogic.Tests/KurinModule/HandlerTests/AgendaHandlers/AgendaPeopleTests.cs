using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>
/// The calendar and the board used to read the whole kurin to label a few assignments. These pin
/// down what is read instead: the people the items name, and everyone only when everyone has a row.
/// </summary>
public sealed class AgendaPeopleTests
{
    private static readonly Guid Kurin = Guid.NewGuid();

    private static AgendaItem Item(Guid createdBy, params AgendaAssignment[] assignments) => new()
    {
        KurinKey = Kurin,
        CreatedByUserKey = createdBy,
        Assignments = assignments.ToList()
    };

    private static AgendaAssignment Target(AgendaTargetType type, Guid key, AgendaCompletionMode mode = AgendaCompletionMode.Shared) =>
        new() { TargetType = type, TargetKey = key, CompletionMode = mode };

    [Fact]
    public void ToLabel_NamesTheMembersTargeted_TheAuthors_AndAGroupDoneEachOnTheirOwn()
    {
        var author = Guid.NewGuid();
        var mover = Guid.NewGuid();
        var member = Guid.NewGuid();
        var groupShared = Guid.NewGuid();
        var groupPerMember = Guid.NewGuid();
        var shared = Target(AgendaTargetType.Group, groupShared);
        shared.StatusChangedByUserKey = mover;

        var selection = AgendaPeople.ToLabel([
            Item(author, Target(AgendaTargetType.Member, member), shared, Target(AgendaTargetType.Group, groupPerMember, AgendaCompletionMode.PerMember))
        ]);

        selection.Should().NotBeNull();
        selection!.MemberKeys.Should().Equal(member);
        selection.GroupKeys.Should().ContainSingle("a гурток closed as a whole is a label, its people are not listed").Which.Should().Be(groupPerMember);
        selection.AccountKeys.Should().BeEquivalentTo([author, mover]);
    }

    [Fact]
    public void ToLabel_NeedsEveryone_WhenTheWholeKurinIsDoneEachOnTheirOwn()
    {
        var selection = AgendaPeople.ToLabel([Item(Guid.NewGuid(), Target(AgendaTargetType.Kurin, Kurin, AgendaCompletionMode.PerMember))]);

        selection.Should().BeNull();
    }

    [Fact]
    public void ToLabel_ForAWholeKurinTargetClosedAsOne_ReadsNobodyForIt()
    {
        var author = Guid.NewGuid();

        var selection = AgendaPeople.ToLabel([Item(author, Target(AgendaTargetType.Kurin, Kurin))]);

        selection.Should().NotBeNull();
        selection!.MemberKeys.Should().BeEmpty();
        selection.GroupKeys.Should().BeEmpty();
        selection.AccountKeys.Should().Equal(author);
    }

    [Fact]
    public void ToNotify_ReachesTheMembersAndGroupsAimedAt_AndEveryoneOnlyForTheKurin()
    {
        var member = Guid.NewGuid();
        var group = Guid.NewGuid();

        var some = AgendaPeople.ToNotify(Item(Guid.NewGuid(), Target(AgendaTargetType.Member, member), Target(AgendaTargetType.Group, group)));
        var everyone = AgendaPeople.ToNotify(Item(Guid.NewGuid(), Target(AgendaTargetType.Group, group), Target(AgendaTargetType.Kurin, Kurin)));

        some.Should().NotBeNull();
        some!.MemberKeys.Should().Equal(member);
        some.GroupKeys.Should().Equal(group);
        some.AccountKeys.Should().BeEmpty();
        everyone.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_AsksForTheSelection_AndForTheKurinOnlyWithoutOne()
    {
        var directory = new Mock<IMemberDirectory>();
        directory.Setup(d => d.GetByKurinAsync(Kurin, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        directory.Setup(d => d.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var selection = new MemberSelection([Guid.NewGuid()], [], []);

        await AgendaPeople.ReadAsync(directory.Object, Kurin, selection, CancellationToken.None);
        directory.Verify(d => d.GetByKurinAsync(Kurin, selection, It.IsAny<CancellationToken>()), Times.Once);
        directory.Verify(d => d.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>()), Times.Never);

        await AgendaPeople.ReadAsync(directory.Object, Kurin, null, CancellationToken.None);
        directory.Verify(d => d.GetByKurinAsync(Kurin, It.IsAny<CancellationToken>()), Times.Once);
    }
}
