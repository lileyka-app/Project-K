using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>
/// A senior ступінь the person enters on their own card waits for the Звʼязковий and counts for
/// nothing until then; the провід's entries and every юнацький ступінь count at once.
/// </summary>
public class PlastLevelConfirmationTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();

    [Theory]
    [InlineData(PlastLevel.Starshoplastun, true, true)]
    [InlineData(PlastLevel.SeniorPratsi, true, true)]
    [InlineData(PlastLevel.Skob, true, false)]
    [InlineData(PlastLevel.HetmanskiySkob, true, false)]
    [InlineData(PlastLevel.Starshoplastun, false, false)]
    [InlineData(PlastLevel.SeniorKerivnytstva, false, false)]
    public void WaitsForConfirmation_OnlyForASeniorLevelTheyGaveThemselves(PlastLevel level, bool byThemselves, bool waits)
    {
        PlastLevelHistoryRules.WaitsForConfirmation(level, byThemselves).Should().Be(waits);
    }

    [Fact]
    public void Apply_ByThemselves_AddsASeniorLevelWaiting_AndAYouthOneCounting()
    {
        var history = new List<PlastLevelHistory>();

        PlastLevelHistoryRules.Apply(Guid.NewGuid(), KurinKey, new List<PlastLevelHistoryDto>
        {
            new() { PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2020, 1, 1) },
            new() { PlastLevel = PlastLevel.Starshoplastun, DateAchieved = new DateOnly(2024, 1, 1) }
        }, history, enteredByThemselves: true);

        history.Single(h => h.PlastLevel == PlastLevel.Skob).AwaitsConfirmation.Should().BeFalse();
        history.Single(h => h.PlastLevel == PlastLevel.Starshoplastun).AwaitsConfirmation.Should().BeTrue();
        PlastLevelHistoryRules.LatestOf(history).Should().Be(PlastLevel.Skob, "a waiting ступінь is not the latest that counts");
    }

    [Fact]
    public void Apply_ByTheProvid_CountsASeniorLevelAtOnce()
    {
        var history = new List<PlastLevelHistory>();

        PlastLevelHistoryRules.Apply(Guid.NewGuid(), KurinKey, new List<PlastLevelHistoryDto>
        {
            new() { PlastLevel = PlastLevel.Senior, DateAchieved = new DateOnly(2024, 1, 1) }
        }, history);

        history.Single().AwaitsConfirmation.Should().BeFalse();
        PlastLevelHistoryRules.LatestOf(history).Should().Be(PlastLevel.Senior);
    }

    [Fact]
    public void Apply_ByThemselves_TurningAConfirmedRowSenior_MakesItWait_AndKeepsAnUntouchedOneAsItWas()
    {
        var moved = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2022, 1, 1) };
        var waiting = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Senior, DateAchieved = new DateOnly(2025, 1, 1), AwaitsConfirmation = true };
        var history = new List<PlastLevelHistory> { moved, waiting };

        PlastLevelHistoryRules.Apply(Guid.NewGuid(), KurinKey, new List<PlastLevelHistoryDto>
        {
            new() { PlastLevelHistoryKey = moved.PlastLevelHistoryKey, PlastLevel = PlastLevel.Starshoplastun, DateAchieved = moved.DateAchieved },
            new() { PlastLevelHistoryKey = waiting.PlastLevelHistoryKey, PlastLevel = PlastLevel.Senior, DateAchieved = waiting.DateAchieved }
        }, history, enteredByThemselves: true);

        moved.AwaitsConfirmation.Should().BeTrue();
        waiting.AwaitsConfirmation.Should().BeTrue();
        PlastLevelHistoryRules.LatestOf(history).Should().BeNull();
    }

    [Fact]
    public void Apply_ByTheProvid_LeavesAWaitingRowWaiting_UnlessTheyChangeItsLevel()
    {
        var untouched = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Senior, DateAchieved = new DateOnly(2025, 1, 1), AwaitsConfirmation = true };
        var changed = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Starshoplastun, DateAchieved = new DateOnly(2024, 1, 1), AwaitsConfirmation = true };
        var history = new List<PlastLevelHistory> { untouched, changed };

        PlastLevelHistoryRules.Apply(Guid.NewGuid(), KurinKey, new List<PlastLevelHistoryDto>
        {
            new() { PlastLevelHistoryKey = untouched.PlastLevelHistoryKey, PlastLevel = PlastLevel.Senior, DateAchieved = untouched.DateAchieved },
            new() { PlastLevelHistoryKey = changed.PlastLevelHistoryKey, PlastLevel = PlastLevel.SeniorPratsi, DateAchieved = changed.DateAchieved }
        }, history);

        untouched.AwaitsConfirmation.Should().BeTrue();
        changed.AwaitsConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ToDto_CarriesTheWaitingMark()
    {
        var waiting = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Senior, AwaitsConfirmation = true };

        waiting.ToDto().AwaitsConfirmation.Should().BeTrue();
    }

    [Fact]
    public void Trail_MarksAWaitingLevel_SoAConfirmationIsAChange()
    {
        var entry = new PlastLevelHistory { PlastLevel = PlastLevel.Senior, DateAchieved = new DateOnly(2025, 1, 1), AwaitsConfirmation = true };
        var waiting = MemberTrail.PlastLevelsOf([entry]);

        entry.AwaitsConfirmation = false;

        MemberTrail.PlastLevelsOf([entry]).Should().NotBe(waiting);
        waiting.Should().Contain("чекає підтвердження");
    }
}
