using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>
/// The tab opens when the second probe closed unless a date is written down, and the вмілості
/// confirmed from that day on are the ones that count for the probe.
/// </summary>
public class ThirdProbeReaderTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProbeProjectRepository> _projects = new();
    private readonly Mock<IProbeProgressRepository> _progress = new();
    private readonly Mock<IBadgeProgressRepository> _badges = new();
    private readonly Mock<IProbeProjectAccess> _access = new();
    private readonly Mock<IMemberProbeLine> _line = new();
    private readonly Mock<IBadgesCatalogService> _catalog = new();
    private readonly Mock<IBlobReadLinks> _links = new();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly ThirdProbeReader _reader;

    public ThirdProbeReaderTests()
    {
        _unitOfWork.SetupGet(u => u.ProbeProjects).Returns(_projects.Object);
        _unitOfWork.SetupGet(u => u.ProbeProgresses).Returns(_progress.Object);
        _unitOfWork.SetupGet(u => u.BadgeProgresses).Returns(_badges.Object);
        _projects.Setup(p => p.GetForMemberProbeAsync(_memberKey, "probe-3", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _line.Setup(l => l.ForMemberAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ProbeSummaryResponse("probe-1", "Перша", 10, 2, null, 1),
            new ProbeSummaryResponse("probe-2", "Друга", 10, 2, null, 2),
            new ProbeSummaryResponse("probe-3", "Третя", 0, 0, null, 3)
        ]);
        _progress.Setup(p => p.GetByMemberAndProbeIdAsync(_memberKey, "probe-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeProgress { ProbeId = "probe-2", Status = ProbeProgressStatus.Completed, CompletedAtUtc = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc) });
        _badges.Setup(b => b.GetByMemberKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Badge("swimmer", new DateTime(2025, 6, 1)),
            Badge("biker", new DateTime(2026, 4, 10)),
            new BadgeProgress { BadgeId = "pending", Status = BadgeProgressStatus.Submitted }
        ]);
        _catalog.Setup(c => c.GetBadgeById("biker")).Returns(new Badge("biker", "Велосипедист", "img", "", "Велосипедна", "", null, "", "", "", [], [], "", []));
        _reader = new ThirdProbeReader(_unitOfWork.Object, _access.Object, _line.Object, _catalog.Object, _links.Object);
    }

    private BadgeProgress Badge(string id, DateTime confirmedAt) =>
        new() { MemberKey = _memberKey, BadgeId = id, Status = BadgeProgressStatus.Confirmed, ReviewedAtUtc = confirmedAt };

    [Fact]
    public async Task OpensWhenTheSecondProbeClosed_AndCountsOnlyVmilostiFromThen()
    {
        var tab = await _reader.ReadAsync(_memberKey, "probe-3");

        tab.IsProjectProbe.Should().BeTrue();
        tab.File!.OpenedOn.Should().Be(new DateOnly(2026, 3, 1));
        tab.File.OpenedOnIsDerived.Should().BeTrue();
        tab.Badges.Select(b => (b.BadgeId, b.DuringProbe)).Should().BeEquivalentTo([("biker", true), ("swimmer", false)]);
        tab.Badges.Single(b => b.BadgeId == "biker").Title.Should().Be("Велосипедист");
    }

    [Fact]
    public async Task AWrittenOpeningDate_WinsOverTheSecondProbe()
    {
        _projects.Setup(p => p.GetFileAsync(_memberKey, "probe-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThirdProbeFile { OpenedOn = new DateOnly(2025, 1, 1) });

        var tab = await _reader.ReadAsync(_memberKey, "probe-3");

        tab.File!.OpenedOnIsDerived.Should().BeFalse();
        tab.Badges.Should().OnlyContain(b => b.DuringProbe);
    }

    [Fact]
    public async Task AnyOtherProbe_HasNoTab()
    {
        var tab = await _reader.ReadAsync(_memberKey, "probe-2");

        tab.IsProjectProbe.Should().BeFalse();
        tab.File.Should().BeNull();
    }
}
