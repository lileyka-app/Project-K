using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>A probe starts only in its turn, and a closed first probe holds a person to its edition.</summary>
public class MemberProbeLineTests
{
    private const string New = "upyu-2026";

    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Mock<IProbeProgressRepository> _progress = new();
    private readonly Mock<IGroupProbeEditions> _groupEditions = new();
    private readonly MemberProbeLine _line;

    public MemberProbeLineTests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.ProbeProgresses).Returns(_progress.Object);
        var catalog = new Mock<IProbesCatalogService>();
        catalog.Setup(x => x.GetProbes()).Returns(
        [
            Probe("probe-1", 1),
            Probe("probe-2", 2),
            Probe("probe-1-2026", 1, New),
            Probe("probe-2-2026", 2, New),
        ]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.FindKurinKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_kurinKey);
        Given();

        _line = new MemberProbeLine(unitOfWork.Object, catalog.Object, members.Object, _groupEditions.Object);
    }

    private static ProbeSummaryResponse Probe(string id, int order, string? edition = null) =>
        new(id, id, 10, 2, edition, order, "ua", "approved");

    private void Given(params (string Id, ProbeProgressStatus Status)[] rows)
    {
        var progress = rows.Select(row => new ProbeProgress { MemberKey = _memberKey, ProbeId = row.Id, Status = row.Status }).ToList();
        _progress.Setup(x => x.GetByMemberKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(progress);
        _progress.Setup(x => x.GetByMemberKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(progress);
    }

    [Fact]
    public async Task CanStart_LetsTheFirstProbeStart()
    {
        (await _line.CanStartAsync(_memberKey, "probe-1")).Should().Be(ProbeStart.Allowed);
    }

    [Fact]
    public async Task CanStart_KeepsTheSecondWaiting_UntilTheFirstIsClosed()
    {
        Given(("probe-1", ProbeProgressStatus.InProgress));
        (await _line.CanStartAsync(_memberKey, "probe-2")).Should().Be(ProbeStart.WaitsForPrevious);

        Given(("probe-1", ProbeProgressStatus.Completed));
        (await _line.CanStartAsync(_memberKey, "probe-2")).Should().Be(ProbeStart.Allowed);
    }

    [Fact]
    public async Task CanStart_RefusesTheNewSecondProbe_ToSomeoneHeldByTheOldFirst()
    {
        _groupEditions.Setup(x => x.ForMemberAsync(_memberKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(New);
        Given(("probe-1", ProbeProgressStatus.Verified));

        (await _line.CanStartAsync(_memberKey, "probe-2-2026")).Should().Be(ProbeStart.NotInLine);
        (await _line.CanStartAsync(_memberKey, "probe-2")).Should().Be(ProbeStart.Allowed);
    }

    [Fact]
    public async Task ForMember_FollowsTheGroupsEdition()
    {
        _groupEditions.Setup(x => x.ForMemberAsync(_memberKey, _kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(New);

        (await _line.ForMemberAsync(_memberKey)).Select(p => p.Id).Should().Equal("probe-1-2026", "probe-2-2026");
    }

    [Fact]
    public async Task ForMembers_SaysWhoIsHeld()
    {
        Given(("probe-1", ProbeProgressStatus.Verified), ("probe-2", ProbeProgressStatus.InProgress));

        var holds = await ((IProbeEditionHolds)_line).ForMembersAsync([_memberKey]);

        holds[_memberKey].Should().Be(new ProbeEditionHold(_memberKey, null, "probe-1", IsHeld: true));
    }

    [Fact]
    public async Task ForMembers_SaysWhoOnlyHasAProbeUnderWay()
    {
        Given(("probe-1", ProbeProgressStatus.InProgress));

        var holds = await ((IProbeEditionHolds)_line).ForMembersAsync([_memberKey]);

        holds[_memberKey].Should().Be(new ProbeEditionHold(_memberKey, null, "probe-1", IsHeld: false));
    }

    [Fact]
    public async Task ForMembers_LeavesOutWhoeverHasNothingStarted()
    {
        Given(("probe-1", ProbeProgressStatus.NotStarted));

        (await ((IProbeEditionHolds)_line).ForMembersAsync([_memberKey])).Should().BeEmpty();
    }
}
