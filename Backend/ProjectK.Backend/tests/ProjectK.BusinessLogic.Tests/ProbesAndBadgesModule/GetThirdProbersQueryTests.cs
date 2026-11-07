using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Project;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>
/// What the referent's tool reads: third-probers with a file or a closed second probe, only those
/// the caller may sign, with their stages and counts.
/// </summary>
public class GetThirdProbersQueryTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProbeProjectRepository> _projects = new();
    private readonly Mock<IProbeProgressRepository> _progress = new();
    private readonly Mock<IBadgeProgressRepository> _badges = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IMemberProbeLine> _line = new();
    private readonly Mock<IProbeProjectAccess> _access = new();
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly MemberSummary _withFile;
    private readonly MemberSummary _justClosed;
    private readonly MemberSummary _stillOnSecond;
    private readonly MemberSummary _notMine;

    public GetThirdProbersQueryTests()
    {
        _withFile = Person("Віталій", "Романюк");
        _justClosed = Person("Остап", "Бондаренко");
        _stillOnSecond = Person("Ігор", "Савчук");
        _notMine = Person("Левко", "Шевченко");
        var everyone = new[] { _withFile, _justClosed, _stillOnSecond, _notMine };

        _unitOfWork.SetupGet(u => u.ProbeProjects).Returns(_projects.Object);
        _unitOfWork.SetupGet(u => u.ProbeProgresses).Returns(_progress.Object);
        _unitOfWork.SetupGet(u => u.BadgeProgresses).Returns(_badges.Object);
        _members.Setup(m => m.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(everyone);
        _line.Setup(l => l.ForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ProbeSummaryResponse("probe-2", "Друга", 10, 2, null, 2),
            new ProbeSummaryResponse("probe-3", "Третя", 0, 0, null, 3)
        ]);
        _access.Setup(a => a.CanSignAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _access.Setup(a => a.CanSignAsync(_notMine.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        _projects.Setup(p => p.GetFilesForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ThirdProbeFile
            {
                MemberKey = _withFile.MemberKey, ProbeId = "probe-3", OpenedOn = new DateOnly(2026, 1, 1),
                ApplicationSubmittedOn = new DateOnly(2025, 12, 1), CampName = "КВТ", Resume = "Шлях"
            },
            new ThirdProbeFile { MemberKey = _notMine.MemberKey, ProbeId = "probe-3" }
        ]);
        _projects.Setup(p => p.GetProjectsForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ProbeProject { MemberKey = _withFile.MemberKey, ProbeId = "probe-3", CompletedAtUtc = DateTime.UtcNow },
            new ProbeProject { MemberKey = _withFile.MemberKey, ProbeId = "probe-3" },
            new ProbeProject { MemberKey = _withFile.MemberKey, ProbeId = "probe-3", IsSpare = true }
        ]);
        _progress.Setup(p => p.GetByMemberKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ProbeProgress { MemberKey = _justClosed.MemberKey, ProbeId = "probe-2", CompletedAtUtc = new DateTime(2026, 5, 1) },
            new ProbeProgress { MemberKey = _stillOnSecond.MemberKey, ProbeId = "probe-2", Status = ProbeProgressStatus.InProgress }
        ]);
        _badges.Setup(b => b.GetByMemberKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BadgeProgress { MemberKey = _withFile.MemberKey, Status = BadgeProgressStatus.Confirmed, ReviewedAtUtc = new DateTime(2026, 2, 1) },
            new BadgeProgress { MemberKey = _withFile.MemberKey, Status = BadgeProgressStatus.Confirmed, ReviewedAtUtc = new DateTime(2025, 2, 1) }
        ]);
    }

    private MemberSummary Person(string first, string last) => new(Guid.NewGuid(), null, _kurinKey, null, first, last, "x@example.com", null);

    [Fact]
    public async Task ListsTheThirdProbersTheCallerMaySign_WithStagesAndCounts()
    {
        var handler = new GetThirdProbersQueryHandler(_unitOfWork.Object, _members.Object, _line.Object, _access.Object);

        var result = await handler.Handle(new GetThirdProbersQuery(_kurinKey), CancellationToken.None);

        result.Data!.Select(p => p.LastName).Should().Equal("Бондаренко", "Романюк");
        var vitalii = result.Data!.Single(p => p.MemberKey == _withFile.MemberKey);
        vitalii.Stages.ApplicationSubmittedOn.Should().Be(new DateOnly(2025, 12, 1));
        vitalii.Stages.OpenedOn.Should().Be(new DateOnly(2026, 1, 1));
        (vitalii.MainProjects, vitalii.CompletedProjects, vitalii.BadgesDuringProbe).Should().Be((2, 1, 1));
        (vitalii.HasCamp, vitalii.HasResume, vitalii.ReflectionConfirmed).Should().Be((true, true, false));
        result.Data!.Single(p => p.MemberKey == _justClosed.MemberKey).Stages.OpenedOn.Should().BeNull();
    }
}
