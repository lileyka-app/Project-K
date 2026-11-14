using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>
/// Who moves which part of a task, and what the parts add up to (AGENDA-02): seeing a task through
/// one's гурток is not the right to close it, and a task done «кожному окремо» is done when everyone is.
/// </summary>
public class AgendaCompletionTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly Guid Sokoly = Guid.NewGuid();
    private static readonly Guid Vedmedi = Guid.NewGuid();
    private static readonly Guid KurinLeadership = Guid.NewGuid();

    private static readonly Guid Oksana = Guid.NewGuid();
    private static readonly Guid Petro = Guid.NewGuid();
    private static readonly Guid Marta = Guid.NewGuid();
    private static readonly Guid Ivan = Guid.NewGuid();

    private static readonly AgendaRoster Roster = new(
        new Dictionary<Guid, Guid?> { [Oksana] = Sokoly, [Petro] = Sokoly, [Marta] = Sokoly, [Ivan] = Vedmedi },
        new Dictionary<Guid, IReadOnlyList<Guid>> { [KurinLeadership] = [Ivan] });

    private static AgendaItem Task(params AgendaAssignment[] assignments) => new()
    {
        AgendaItemKey = Guid.NewGuid(),
        KurinKey = Kurin,
        Kind = AgendaItemKind.Task,
        CreatedByUserKey = Author,
        Assignments = assignments.ToList()
    };

    private static AgendaAssignment To(AgendaTargetType type, Guid key, AgendaCompletionMode mode = AgendaCompletionMode.Shared) =>
        new() { TargetType = type, TargetKey = key, CompletionMode = mode };

    /// <summary>A youth of a гурток, with no office.</summary>
    private static AgendaViewerContext Youth(Guid member, Guid group) =>
        new(Kurin, Guid.NewGuid(), member, group, [group], [], false, false);

    /// <summary>The гуртковий: a youth of the гурток who also runs it.</summary>
    private static AgendaViewerContext Hurtkovyi(Guid member, Guid group) =>
        new(Kurin, Guid.NewGuid(), member, group, [group], [], false, true, [group]);

    private static AgendaViewerContext TheAuthor() =>
        new(Kurin, Author, null, null, [], [], false, true);

    private static AgendaViewerContext Zvyazkovyi() =>
        new(Kurin, Guid.NewGuid(), null, null, [], [], true, true);

    private static void Part(AgendaAssignment assignment, Guid member, AgendaItemStatus status) =>
        assignment.Progress.Add(new AgendaAssignmentProgress { MemberKey = member, Status = status });

    [Fact]
    public void AYouth_SeesAGurtokTask_ButOnlyItsProvidClosesIt()
    {
        var target = To(AgendaTargetType.Group, Sokoly);
        var item = Task(target);

        AgendaCompletion.CanMoveTarget(item, target, Youth(Oksana, Sokoly)).Should().BeFalse();
        AgendaCompletion.BoardMove(item, Youth(Oksana, Sokoly)).Should().BeEmpty();
        AgendaCompletion.CanMoveTarget(item, target, Hurtkovyi(Petro, Sokoly)).Should().BeTrue();
        AgendaCompletion.CanMoveTarget(item, target, TheAuthor()).Should().BeTrue();
        AgendaCompletion.CanMoveTarget(item, target, Zvyazkovyi()).Should().BeTrue();
    }

    [Fact]
    public void ClosedByAnyone_LetsEveryoneInTheGurtokCloseIt_AndNoOneOutside()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.SharedByAnyone);
        var item = Task(target);

        AgendaCompletion.CanMoveTarget(item, target, Youth(Oksana, Sokoly)).Should().BeTrue();
        AgendaCompletion.CanMoveTarget(item, target, Youth(Ivan, Vedmedi)).Should().BeFalse();
    }

    [Fact]
    public void EachOnTheirOwn_AYouthMovesOnlyTheirPart_TheHurtkovyiAnyone()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);
        var item = Task(target);

        AgendaCompletion.CanMovePart(item, target, Youth(Oksana, Sokoly), Oksana).Should().BeTrue();
        AgendaCompletion.CanMovePart(item, target, Youth(Oksana, Sokoly), Marta).Should().BeFalse();
        AgendaCompletion.CanMovePart(item, target, Hurtkovyi(Petro, Sokoly), Marta).Should().BeTrue();
        AgendaCompletion.CanMoveTarget(item, target, Hurtkovyi(Petro, Sokoly)).Should().BeFalse("the target's state is what the parts add up to");
        AgendaCompletion.StakeOf(item, Youth(Oksana, Sokoly))!.MemberKey.Should().Be(Oksana);
    }

    [Fact]
    public void EachOnTheirOwn_IsDoneOnlyWhenEveryoneNowInTheGurtokIs()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);
        var item = Task(target);

        AgendaCompletion.TargetStatus(target, Roster).Should().Be(AgendaItemStatus.Todo);

        Part(target, Oksana, AgendaItemStatus.Done);
        Part(target, Petro, AgendaItemStatus.Done);
        AgendaCompletion.TargetStatus(target, Roster).Should().Be(AgendaItemStatus.InProgress);

        Part(target, Marta, AgendaItemStatus.Done);
        AgendaCompletion.TargetStatus(target, Roster).Should().Be(AgendaItemStatus.Done);
        AgendaCompletion.ItemStatus(item, Roster).Should().Be(AgendaItemStatus.Done);
    }

    [Fact]
    public void ANewcomer_CountsFromTheDayTheyJoin_AndALeaversPartStaysOutOfTheCount()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);
        Part(target, Oksana, AgendaItemStatus.Done);
        Part(target, Petro, AgendaItemStatus.Done);
        Part(target, Ivan, AgendaItemStatus.Done); // was in Соколи, now in Ведмеді

        // Марта has not done hers: the three rows do not make it done.
        AgendaCompletion.TargetStatus(target, Roster).Should().Be(AgendaItemStatus.InProgress);

        var withoutMarta = new AgendaRoster(
            new Dictionary<Guid, Guid?> { [Oksana] = Sokoly, [Petro] = Sokoly, [Ivan] = Vedmedi },
            new Dictionary<Guid, IReadOnlyList<Guid>>());
        AgendaCompletion.TargetStatus(target, withoutMarta).Should().Be(AgendaItemStatus.Done);
    }

    [Fact]
    public void ATaskForThreeGurtky_StandsInADifferentColumnForEach()
    {
        var sokoly = To(AgendaTargetType.Group, Sokoly);
        var vedmedi = To(AgendaTargetType.Group, Vedmedi);
        sokoly.Status = AgendaItemStatus.Done;
        var item = Task(sokoly, vedmedi);

        AgendaCompletion.ViewerStatus(item, Hurtkovyi(Petro, Sokoly), Roster).Should().Be(AgendaItemStatus.Done);
        AgendaCompletion.ViewerStatus(item, Hurtkovyi(Ivan, Vedmedi), Roster).Should().Be(AgendaItemStatus.Todo);
        AgendaCompletion.ItemStatus(item, Roster).Should().Be(AgendaItemStatus.InProgress, "one гурток closing it does not close it for the other");
    }

    [Fact]
    public void AHolderOfTheKurinsProvid_MovesATaskSetForThatProvid()
    {
        var target = To(AgendaTargetType.Leadership, KurinLeadership);
        var item = Task(target);
        var kurinnyi = new AgendaViewerContext(Kurin, Guid.NewGuid(), Ivan, Vedmedi, [Vedmedi], [KurinLeadership], false, true);

        AgendaCompletion.CanMoveTarget(item, target, kurinnyi).Should().BeTrue();
        AgendaCompletion.BoardMove(item, kurinnyi).Should().ContainSingle();
    }

    [Fact]
    public void ATaskForTheWholeKurin_IsNotEveryMembersToClose()
    {
        var target = To(AgendaTargetType.Kurin, Kurin);
        var item = Task(target);

        AgendaCompletion.CanMoveTarget(item, target, Youth(Oksana, Sokoly)).Should().BeFalse();
        AgendaCompletion.CanMoveTarget(item, target, Zvyazkovyi()).Should().BeTrue();
    }

    [Fact]
    public void TheAuthorsDrag_ClosesSharedTargets_ButNeverMarksEveryoneDoneAtOnce()
    {
        var shared = To(AgendaTargetType.Group, Vedmedi);
        var perMember = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);

        AgendaCompletion.BoardMove(Task(shared, perMember), TheAuthor()).Select(s => s.Assignment).Should().Equal(shared);
        AgendaCompletion.BoardMove(Task(perMember), TheAuthor()).Should().BeEmpty();
    }

    [Fact]
    public void ATaskHandedToAPersonByName_IsTheirsBeforeAnythingTheirGurtokHas()
    {
        var group = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.SharedByAnyone);
        var named = To(AgendaTargetType.Member, Oksana);
        var item = Task(group, named);

        AgendaCompletion.StakeOf(item, Youth(Oksana, Sokoly))!.Assignment.Should().BeSameAs(named);
    }

    [Fact]
    public void AMemberTarget_IsOnePersonsWhateverModeItCarries()
    {
        var named = To(AgendaTargetType.Member, Oksana, AgendaCompletionMode.PerMember);

        AgendaCompletion.ModeOf(named).Should().Be(AgendaCompletionMode.Shared);
        AgendaCompletion.CanMoveTarget(Task(named), named, Youth(Oksana, Sokoly)).Should().BeTrue();
    }

    [Fact]
    public void ATargetHasMoved_OnceItsStateOrAnyPartHas()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);
        AgendaCompletion.HasMoved(target).Should().BeFalse();

        Part(target, Oksana, AgendaItemStatus.InProgress);
        AgendaCompletion.HasMoved(target).Should().BeTrue();
    }

    // Found live: the гуртковий handed in his own вкладка and the whole task jumped to «Зроблено» for him.
    [Fact]
    public void TheHurtkovyi_SeesATaskDoneEachOnTheirOwn_ByTheWholeGurtok_NotByHisOwnPart()
    {
        var target = To(AgendaTargetType.Group, Sokoly, AgendaCompletionMode.PerMember);
        var item = Task(target);
        Part(target, Petro, AgendaItemStatus.Done);
        target.Status = AgendaCompletion.TargetStatus(target, Roster);

        var hurtkovyi = Hurtkovyi(Petro, Sokoly);
        AgendaCompletion.ViewerStatus(item, hurtkovyi, Roster).Should().Be(AgendaItemStatus.InProgress);
        AgendaCompletion.BoardMove(item, hurtkovyi).Should().BeEmpty("parts are marked one by one from the dialog");
        AgendaCompletion.CanMovePart(item, target, hurtkovyi, Petro).Should().BeTrue();
    }
}
