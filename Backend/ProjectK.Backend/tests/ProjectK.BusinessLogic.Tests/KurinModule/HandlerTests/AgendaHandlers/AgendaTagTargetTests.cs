using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>
/// An item aimed at a tag reaches whoever carries the tag at the time it is read — like a гурток,
/// so someone given the tag later finds what was assigned to it, and someone without it never does.
/// </summary>
public class AgendaTagTargetTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();
    private static readonly Guid Novices = Guid.NewGuid();

    private static AgendaItem AimedAt(AgendaTargetType type, Guid key)
    {
        var item = new AgendaItem { KurinKey = KurinKey, Title = "Сходини новиків" };
        item.Assignments.Add(new AgendaAssignment { AgendaItem = item, TargetType = type, TargetKey = key });
        return item;
    }

    private static AgendaViewerScope Viewer(params Guid[] tags) =>
        new(KurinKey, Guid.NewGuid(), Guid.NewGuid(), [], [], false, tags);

    [Fact]
    public void AnItemAimedAtATag_ReachesWhoeverCarriesIt_AndNobodyElse()
    {
        var item = AimedAt(AgendaTargetType.Tag, Novices);

        AgendaVisibility.IsVisible(item, Viewer(Novices)).Should().BeTrue();
        AgendaVisibility.IsVisible(item, Viewer(Guid.NewGuid())).Should().BeFalse();
        AgendaVisibility.IsVisible(item, Viewer()).Should().BeFalse();
    }

    [Fact]
    public void AViewerWithoutTagKeys_IsReachedByNoTag()
    {
        var item = AimedAt(AgendaTargetType.Tag, Novices);
        var noTags = new AgendaViewerScope(KurinKey, Guid.NewGuid(), Guid.NewGuid(), [], [], false);

        AgendaVisibility.IsVisible(item, noTags).Should().BeFalse();
    }

    [Fact]
    public void TheCarrierIsOneOfThePeopleTheTargetStandsFor()
    {
        var item = AimedAt(AgendaTargetType.Tag, Novices);
        var carrier = new AgendaViewerContext(KurinKey, Guid.NewGuid(), Guid.NewGuid(), null, [], [], false, false, ViewerTagKeysOrNull: [Novices]);
        var other = carrier with { ViewerTagKeysOrNull = [] };

        AgendaCompletion.IsIn(item.Assignments.Single(), carrier).Should().BeTrue();
        AgendaCompletion.IsIn(item.Assignments.Single(), other).Should().BeFalse();
    }

    /// <summary>
    /// A task done «кожному окремо» for a tag names its carriers only as far as the viewer may see tags:
    /// whoever oversees the task but is shown no tags learns neither the names nor how many there are.
    /// </summary>
    [Theory]
    [InlineData(CapabilityReach.None, new string[0])]
    [InlineData(CapabilityReach.OwnGroup, new[] { "Сокіл" })]
    [InlineData(CapabilityReach.Kurin, new[] { "Ведмідь", "Сокіл" })]
    public void PerPersonParts_OfATagTarget_ShowOnlyCarriersTheViewerMaySee(CapabilityReach reach, string[] expected)
    {
        var sokoly = Guid.NewGuid();
        var vedmedi = Guid.NewGuid();
        var sokil = Guid.NewGuid();
        var vedmid = Guid.NewGuid();
        var item = new AgendaItem { KurinKey = KurinKey, Title = "Підготувати сходини", Kind = AgendaItemKind.Task, CreatedByUserKey = Guid.NewGuid() };
        item.Assignments.Add(new AgendaAssignment
        {
            AgendaItem = item, TargetType = AgendaTargetType.Tag, TargetKey = Novices, CompletionMode = AgendaCompletionMode.PerMember
        });
        var roster = new AgendaRoster(
            new Dictionary<Guid, Guid?> { [sokil] = sokoly, [vedmid] = vedmedi },
            new Dictionary<Guid, IReadOnlyList<Guid>>(),
            new Dictionary<Guid, IReadOnlyList<Guid>> { [Novices] = [sokil, vedmid] });
        var overseer = new AgendaViewerContext(KurinKey, Guid.NewGuid(), Guid.NewGuid(), null, [sokoly], [], true, true, TagCarriersReach: reach);

        var dto = AgendaItemResponseFactory.Create(item, overseer, "Весь курінь",
            new Dictionary<Guid, string>(), new Dictionary<Guid, string> { [sokil] = "Сокіл", [vedmid] = "Ведмідь" },
            new Dictionary<Guid, string>(), new Dictionary<Guid, string>(), new Dictionary<Guid, AgendaCategory>(), roster)
            .Assignments.Single();

        dto.PeopleCount.Should().Be(expected.Length);
        (dto.Parts ?? []).Select(part => part.Name).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ATagTarget_IsNamedForEveryoneCarryingIt()
    {
        AgendaLookups.TagLabel("Новики").Should().Be("Усі з тегом «Новики»");
    }
}
