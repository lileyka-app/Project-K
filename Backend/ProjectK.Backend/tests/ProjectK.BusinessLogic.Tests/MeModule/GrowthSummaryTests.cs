using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.MeModule;

public class GrowthSummaryTests
{
    private static readonly string[] Catalog = ["first", "second", "third"];

    [Fact]
    public void TheProbaInProgress_ComesFirst_WhateverItsPlaceInTheCatalogue() =>
        GrowthSummary.CurrentProbeId(Catalog, [("first", ProbeProgressStatus.Verified), ("third", ProbeProgressStatus.InProgress), ("second", ProbeProgressStatus.Completed)])
            .Should().Be("third");

    [Fact]
    public void OneWaitingToBeVerified_IsStillTheirs() =>
        GrowthSummary.CurrentProbeId(Catalog, [("first", ProbeProgressStatus.Completed)]).Should().Be("first");

    [Fact]
    public void NothingStarted_PointsAtTheFirstNotYetVerified() =>
        GrowthSummary.CurrentProbeId(Catalog, [("first", ProbeProgressStatus.Verified)]).Should().Be("second");

    [Fact]
    public void EveryProbaVerified_LeavesNothingToShow() =>
        GrowthSummary.CurrentProbeId(Catalog, Catalog.Select(id => (id, ProbeProgressStatus.Verified))).Should().BeNull();
}
