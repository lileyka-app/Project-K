using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>
/// The rules a profile write runs on the values it carries — ступені, phones, what counts as a
/// significant change — and the journal entry it leaves.
/// </summary>
public class MemberProfileWriteRulesTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();

    private static Member Stored() => new()
    {
        MemberKey = Guid.NewGuid(),
        FirstName = "Оксана",
        LastName = "Тестова",
        Email = "old@example.com",
        PhoneNumber = "0501234567",
        DateOfBirth = new DateOnly(2010, 5, 17),
        MotherPhone = "+380500000001"
    };

    private static UpsertMemberProfileCommand SameAs(Member member) => new()
    {
        MemberKey = member.MemberKey,
        FirstName = member.FirstName,
        MiddleName = member.MiddleName ?? string.Empty,
        LastName = member.LastName,
        Email = member.Email,
        PhoneNumber = member.PhoneNumber,
        DateOfBirth = member.DateOfBirth,
        MotherPhone = member.MotherPhone,
        PlastLevelHistories = member.PlastLevelHistory
            .Select(entry => new PlastLevelHistoryDto
            {
                PlastLevelHistoryKey = entry.PlastLevelHistoryKey,
                PlastLevel = entry.PlastLevel,
                DateAchieved = entry.DateAchieved
            })
            .ToList()
    };

    [Fact]
    public void Apply_AddsKeylessRows_UpdatesKeyedOnes_AndDropsWhatWasNotSent()
    {
        var memberKey = Guid.NewGuid();
        var kept = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Entry, DateAchieved = new DateOnly(2020, 1, 1) };
        var dropped = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Prykhylnyk, DateAchieved = new DateOnly(2021, 1, 1) };
        var history = new List<PlastLevelHistory> { kept, dropped };

        PlastLevelHistoryRules.Apply(memberKey, KurinKey, new List<PlastLevelHistoryDto>
        {
            new() { PlastLevelHistoryKey = kept.PlastLevelHistoryKey, PlastLevel = PlastLevel.Entry, DateAchieved = new DateOnly(2020, 2, 2) },
            new() { PlastLevel = PlastLevel.Uchasnyk, DateAchieved = new DateOnly(2022, 3, 3), IsPlastunka = true }
        }, history);

        history.Should().HaveCount(2);
        history.Should().NotContain(dropped);
        kept.DateAchieved.Should().Be(new DateOnly(2020, 2, 2));
        history.Should().ContainSingle(entry => entry.PlastLevel == PlastLevel.Uchasnyk
            && entry.MemberKey == memberKey && entry.KurinKey == KurinKey && entry.IsPlastunka);
        PlastLevelHistoryRules.LatestOf(history).Should().Be(PlastLevel.Uchasnyk);
    }

    [Fact]
    public void Apply_WithNothingSent_ClearsTheWay()
    {
        var history = new List<PlastLevelHistory> { new() { PlastLevel = PlastLevel.Entry } };

        PlastLevelHistoryRules.Apply(Guid.NewGuid(), KurinKey, new List<PlastLevelHistoryDto>(), history);

        history.Should().BeEmpty();
        PlastLevelHistoryRules.LatestOf(history).Should().BeNull();
    }

    [Fact]
    public void Differs_IsFalseForTheSameRows_AndTrueForANewOrMovedOne()
    {
        var entry = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Entry, DateAchieved = new DateOnly(2020, 1, 1) };
        var same = new List<PlastLevelHistoryDto> { new() { PlastLevelHistoryKey = entry.PlastLevelHistoryKey, PlastLevel = PlastLevel.Entry, DateAchieved = entry.DateAchieved } };
        var moved = new List<PlastLevelHistoryDto> { new() { PlastLevelHistoryKey = entry.PlastLevelHistoryKey, PlastLevel = PlastLevel.Entry, DateAchieved = new DateOnly(2020, 6, 1) } };
        var added = new List<PlastLevelHistoryDto> { new() { PlastLevel = PlastLevel.Entry, DateAchieved = entry.DateAchieved } };

        PlastLevelHistoryRules.Differs(same, [entry]).Should().BeFalse();
        PlastLevelHistoryRules.Differs(moved, [entry]).Should().BeTrue();
        PlastLevelHistoryRules.Differs(added, [entry]).Should().BeTrue();
    }

    [Fact]
    public void TryNormalizePhones_LetsAStoredNumberThrough_AndShapesANewOne()
    {
        var stored = Stored();
        var request = SameAs(stored);

        MemberProfileWriteRules.TryNormalizePhones(request, stored).Should().BeTrue();
        request.PhoneNumber.Should().Be("0501234567");

        request.PhoneNumber = "+380 50 765 43 21";
        request.FatherPhone = "+420 777 123 456";
        MemberProfileWriteRules.TryNormalizePhones(request, stored).Should().BeTrue();
        request.PhoneNumber.Should().Be("+380507654321");
        request.FatherPhone.Should().Be("+420777123456");
        request.MotherPhone.Should().Be("+380500000001");
    }

    [Fact]
    public void TryNormalizePhones_RefusesAParentNumberWithoutACountry()
    {
        var stored = Stored();
        var request = SameAs(stored);
        request.MotherPhone = "0501112233";

        MemberProfileWriteRules.TryNormalizePhones(request, stored).Should().BeFalse();
    }

    [Fact]
    public void IsSignificantChange_CountsTheWayOnlyWhenTheWriterMayChangeIt()
    {
        var stored = Stored();
        var membership = new Membership { KurinKey = KurinKey, GroupKey = null };
        var request = SameAs(stored);

        MemberProfileWriteRules.IsSignificantChange(request, stored, membership, KurinKey, null, plastLevelsCount: true).Should().BeFalse();

        request.PlastLevelHistories.Add(new PlastLevelHistoryDto { PlastLevel = PlastLevel.Entry, DateAchieved = new DateOnly(2020, 1, 1) });
        MemberProfileWriteRules.IsSignificantChange(request, stored, membership, KurinKey, null, plastLevelsCount: false).Should().BeFalse();
        MemberProfileWriteRules.IsSignificantChange(request, stored, membership, KurinKey, null, plastLevelsCount: true).Should().BeTrue();

        var renamed = SameAs(stored);
        renamed.LastName = "Інша";
        MemberProfileWriteRules.IsSignificantChange(renamed, stored, membership, KurinKey, null, plastLevelsCount: false).Should().BeTrue();
        MemberProfileWriteRules.IsSignificantChange(SameAs(stored), stored, membership, KurinKey, Guid.NewGuid(), plastLevelsCount: false).Should().BeTrue();
    }

    [Fact]
    public void RecordOf_FilesANewCardUnderItsKurin_AndAnEditUnderTheActingOne()
    {
        var member = Stored();
        var acting = Guid.NewGuid();

        var added = MemberTrail.RecordOf(member, before: null, KurinKey, acting);
        added!.Action.Should().Be(AuditActionNames.Member.Added);
        added.Area.Should().Be(AuditArea.Member);
        added.KurinKey.Should().Be(KurinKey);

        var before = MemberTrail.Of(member);
        MemberTrail.RecordOf(member, before, KurinKey, acting).Should().BeNull();

        member.School = "New school";
        var changed = MemberTrail.RecordOf(member, before, KurinKey, acting);
        changed!.Action.Should().Be(AuditActionNames.Member.Changed);
        changed.KurinKey.Should().Be(acting);
        MemberTrail.RecordOf(member, before, KurinKey, actingKurinKey: null)!.KurinKey.Should().Be(KurinKey);
    }
}
