using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>Hiding a duty and bringing it back touch only the caller's own rows.</summary>
public class DutyDismissalCommandTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Someone = Guid.NewGuid();
    private const string Badges = "BadgesToReview:0f8fad5bd9cb469fa16570867728950e:-:-:-";
    private const string Dues = "EntriesToVerify:0f8fad5bd9cb469fa16570867728950e:-:-:-";

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly List<DismissedDuty> _rows = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DutyDismissalCommandTests()
    {
        var repository = new Mock<IDismissedDutyRepository>();
        repository.Setup(r => r.GetToChangeAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid user, string? key, CancellationToken _) => _rows.Where(d => d.UserKey == user && (key == null || d.DutyKey == key)).ToList());
        repository.Setup(r => r.Create(It.IsAny<DismissedDuty>(), It.IsAny<CancellationToken>())).Callback<DismissedDuty, CancellationToken>((d, _) => _rows.Add(d));
        repository.Setup(r => r.Delete(It.IsAny<DismissedDuty>(), It.IsAny<CancellationToken>())).Callback<DismissedDuty, CancellationToken>((d, _) => _rows.Remove(d));
        _unitOfWork.SetupGet(u => u.DismissedDuties).Returns(repository.Object);
    }

    private static ICurrentUserContext Signed(Guid? user) => Mock.Of<ICurrentUserContext>(u => u.UserId == user);

    private DismissDutyCommandHandler Dismisser() => new(_unitOfWork.Object, Signed(User), _time);

    private RestoreDutiesCommandHandler Restorer() => new(_unitOfWork.Object, Signed(User));

    [Fact]
    public async Task Hiding_RecordsTheDutyAsItIs()
    {
        var result = await Dismisser().Handle(new DismissDutyCommand(Badges, "3"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { UserKey = User, DutyKey = Badges, Fingerprint = "3", DismissedAtUtc = _time.GetUtcNow().UtcDateTime });
    }

    [Fact]
    public async Task HidingAgain_TakesTheNewSize_InTheSameRow()
    {
        await Dismisser().Handle(new DismissDutyCommand(Badges, "3"), CancellationToken.None);
        _time.Advance(TimeSpan.FromDays(2));

        await Dismisser().Handle(new DismissDutyCommand(Badges, "5"), CancellationToken.None);

        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Fingerprint = "5", DismissedAtUtc = _time.GetUtcNow().UtcDateTime });
    }

    [Fact]
    public async Task Restoring_OneKey_LeavesTheRest()
    {
        await Dismisser().Handle(new DismissDutyCommand(Badges, "3"), CancellationToken.None);
        await Dismisser().Handle(new DismissDutyCommand(Dues, "1"), CancellationToken.None);

        await Restorer().Handle(new RestoreDutiesCommand(Badges), CancellationToken.None);

        _rows.Should().ContainSingle().Which.DutyKey.Should().Be(Dues);
    }

    [Fact]
    public async Task RestoringAll_TakesOnlyTheCallersOwn()
    {
        _rows.Add(new DismissedDuty { UserKey = Someone, DutyKey = Badges, Fingerprint = "1" });
        await Dismisser().Handle(new DismissDutyCommand(Badges, "3"), CancellationToken.None);
        await Dismisser().Handle(new DismissDutyCommand(Dues, "1"), CancellationToken.None);

        await Restorer().Handle(new RestoreDutiesCommand(null), CancellationToken.None);

        _rows.Should().ContainSingle().Which.UserKey.Should().Be(Someone);
    }

    [Fact]
    public async Task NobodySignedIn_HidesNothing()
    {
        var result = await new DismissDutyCommandHandler(_unitOfWork.Object, Signed(null), _time)
            .Handle(new DismissDutyCommand(Badges, "3"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Unauthorized);
        _rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Badges, true)]
    [InlineData("EventWithoutAttendance:a:-:b:20261006T160000Z", true)]
    [InlineData("Anything:a:-:-:-", false)]
    [InlineData("BadgesToReview:a", false)]
    [InlineData("", false)]
    public void OnlyAKeyTheDutiesReadWrites_IsAccepted(string key, bool valid) =>
        new DismissDutyCommandValidator().Validate(new DismissDutyCommand(key, "1")).IsValid.Should().Be(valid);
}
