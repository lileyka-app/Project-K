using FluentAssertions;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>
/// A person walks the edition their гурток chose, unless a closed probe past the first holds them
/// to another — then an old first probe is never followed by a new second.
/// </summary>
public class ProbeLineTests
{
    private const string New = "upyu-2026";

    private static readonly ProbeSummaryResponse[] Catalog =
    [
        Probe("probe-1", order: 1),
        Probe("probe-2", order: 2),
        Probe("probe-3", order: 3),
        Probe("znak-2026", order: 0, edition: New),
        Probe("probe-1-2026", order: 1, edition: New),
        Probe("probe-2-2026", order: 2, edition: New),
    ];

    private static ProbeSummaryResponse Probe(string id, int order, string? edition = null) =>
        new(id, id, PointsCount: 10, SectionsCount: 2, edition, order, "ua", "approved");

    private static string[] Line(string? groupEdition, params (string Id, ProbeProgressStatus Status)[] progress) =>
        [.. ProbeLine.Of(Catalog, progress.ToDictionary(p => p.Id, p => p.Status), groupEdition).Select(p => p.Id)];

    [Fact]
    public void Of_WalksTheUnnamedEdition_WhenTheGroupChoseNone()
    {
        Line(null).Should().Equal("probe-1", "probe-2", "probe-3");
    }

    [Fact]
    public void Of_WalksTheGroupsEdition()
    {
        Line(New).Should().Equal("znak-2026", "probe-1-2026", "probe-2-2026");
    }

    [Fact]
    public void Of_KeepsSomeoneWhoClosedAFirstProbe_InItsEdition()
    {
        Line(New, ("probe-1", ProbeProgressStatus.Verified)).Should().Equal("probe-1", "probe-2", "probe-3");
        Line(New, ("probe-1", ProbeProgressStatus.Completed)).Should().Equal("probe-1", "probe-2", "probe-3");
    }

    [Fact]
    public void Of_MovesSomeoneWhoseFirstProbeIsStillOpen_WithTheGroup()
    {
        Line(New, ("probe-1", ProbeProgressStatus.InProgress)).Should().Equal("znak-2026", "probe-1-2026", "probe-2-2026");
    }

    [Fact]
    public void Of_DoesNotLetAClosedЗнакHold_SinceItIsNoFirstProbe()
    {
        Line(null, ("znak-2026", ProbeProgressStatus.Verified)).Should().Equal("znak-2026", "probe-1", "probe-2", "probe-3");
    }

    [Fact]
    public void Of_FallsBackToTheUnnamedEdition_WhenTheGroupsOneIsGoneFromTheCatalogue()
    {
        Line("upyu-2030").Should().Equal("probe-1", "probe-2", "probe-3");
    }

    [Fact]
    public void HeldBy_TakesTheFurthestClosedProbe()
    {
        var progress = new Dictionary<string, ProbeProgressStatus>
        {
            ["probe-1"] = ProbeProgressStatus.Verified,
            ["probe-2"] = ProbeProgressStatus.Completed,
        };

        ProbeLine.HeldBy(Catalog, progress)!.Id.Should().Be("probe-2");
        ProbeLine.HeldBy(Catalog, new Dictionary<string, ProbeProgressStatus>()).Should().BeNull();
    }
}
