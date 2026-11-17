using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>A kurin's people as its roster and pickers read them: who is кадра, which office names them, which ступінь is theirs.</summary>
public class RosterMappingTests
{
    [Fact]
    public void ToDto_FromARosterRow_ShouldCarryNoPublicCodeOrParents_AndPlaceTheRowInItsGroup()
    {
        var kurinKey = Guid.NewGuid();
        var row = Row(kurinKey) with
        {
            ProfilePhotoBlobName = "row.jpg",
            Assignments = [new MemberListAssignment("Соколи", kurinKey, null)],
            Warnings = [new MemberWarning()]
        };

        var dto = row.ToDto(FakeBlobReadLinks.Instance);

        dto.GroupKey.Should().BeEmpty();
        dto.KurinKey.Should().Be(row.KurinKey);
        dto.PublicId.Should().BeNull();
        dto.MotherPhone.Should().BeNull();
        dto.Awards.Should().BeEmpty();
        dto.IsStaff.Should().BeTrue();
        dto.MentoredGroupNames.Should().Equal("Соколи");
        dto.Warnings.Should().ContainSingle();
        dto.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("row.jpg"));
    }

    /// <summary>
    /// The roster's кадра is decided here from the stored facts, by the rule the звіт куреня uses
    /// too: a гуртковий or a курінний holds an office and is still a юнак, a revoked закріплення no
    /// longer counts, and a KV office of this kurin does.
    /// </summary>
    [Fact]
    public void ToDto_FromARosterRow_ShouldDecideStaffAndRole_ByTheSharedRules()
    {
        var kurinKey = Guid.NewGuid();
        MemberListOffice Office(LeadershipType type, LeadershipRole role, Guid? officeKurin, DateOnly? until = null) =>
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, type, officeKurin, null, new DateOnly(2024, 1, 1), until, null);

        var hurtkovyi = Row(kurinKey) with { Offices = [Office(LeadershipType.Group, LeadershipRole.Hurtkoviy, null)] };
        var kurinnyi = Row(kurinKey) with { Offices = [Office(LeadershipType.Kurin, LeadershipRole.Kurinnuy, kurinKey)] };
        var handedOver = Row(kurinKey) with { Assignments = [new MemberListAssignment("Соколи", kurinKey, DateTime.UtcNow)] };
        var vykhovnyk = Row(kurinKey) with
        {
            Offices =
            [
                Office(LeadershipType.KV, LeadershipRole.Vykhovnyk, kurinKey),
                Office(LeadershipType.Group, LeadershipRole.Pysar, null)
            ]
        };

        hurtkovyi.ToDto(FakeBlobReadLinks.Instance).IsStaff.Should().BeFalse();
        kurinnyi.ToDto(FakeBlobReadLinks.Instance).IsStaff.Should().BeFalse();
        var handedOverDto = handedOver.ToDto(FakeBlobReadLinks.Instance);
        handedOverDto.IsStaff.Should().BeFalse();
        handedOverDto.MentoredGroupNames.Should().BeEmpty();

        var vykhovnykDto = vykhovnyk.ToDto(FakeBlobReadLinks.Instance);
        vykhovnykDto.IsStaff.Should().BeTrue();
        // Named by the first провід, then role: Kurin, Group, KV in that order.
        vykhovnykDto.UserRole.Should().Be(SystemRoleNames.ForOffice(LeadershipType.Group, LeadershipRole.Pysar));
        vykhovnykDto.LeadershipHistories.Should().HaveCount(2).And.OnlyContain(history => history.KurinKey == kurinKey);
        hurtkovyi.ToDto(FakeBlobReadLinks.Instance).UserRole.Should().Be(SystemRoleNames.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy));
        Row(kurinKey).ToDto(FakeBlobReadLinks.Instance).UserRole.Should().BeNull();
    }

    [Fact]
    public void ToDto_FromARosterRow_ShouldTakeTheNewestLevel_AndLeaveTheCatalogueIdOff()
    {
        var row = Row(Guid.NewGuid()) with
        {
            StoredPlastLevel = PlastLevel.Uchasnyk,
            PlastLevels =
            [
                new PlastLevelHistory { PlastLevel = PlastLevel.Prykhylnyk, DateAchieved = new DateOnly(2022, 1, 1) },
                new PlastLevelHistory { PlastLevel = PlastLevel.Rozviduvach, DateAchieved = new DateOnly(2024, 1, 1) }
            ]
        };

        var dto = row.ToDto(FakeBlobReadLinks.Instance);

        dto.LatestPlastLevel.Should().Be(PlastLevel.Rozviduvach);
        dto.PlastLevelHistories.Should().HaveCount(2).And.OnlyContain(history => history.CatalogId == null);
        (Row(Guid.NewGuid()) with { StoredPlastLevel = PlastLevel.Uchasnyk }).ToDto(FakeBlobReadLinks.Instance)
            .LatestPlastLevel.Should().Be(PlastLevel.Uchasnyk);
    }

    [Fact]
    public void ToLookupDto_FromARow_ShouldNameThePersonByTheirOffice()
    {
        var row = new MemberLookupRow(Guid.NewGuid(), Guid.NewGuid(), "Ірина", "Петрівна", "Нова", "l.jpg",
            new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi));

        var dto = row.ToLookupDto();

        dto.UserRole.Should().Be(SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi));
        dto.MiddleName.Should().Be("Петрівна");
        dto.ProfilePhotoBlobName.Should().Be("l.jpg");
        (row with { Office = null }).ToLookupDto().UserRole.Should().BeNull();
    }

    private static MemberListRow Row(Guid kurinKey) => new()
    {
        MemberKey = Guid.NewGuid(),
        GroupKey = null,
        GroupName = null,
        KurinKey = kurinKey,
        UserKey = null,
        FirstName = "Ірина",
        MiddleName = null,
        LastName = "Нова",
        Email = string.Empty,
        PhoneNumber = string.Empty,
        DateOfBirth = null,
        Address = null,
        School = null,
        StoredPlastLevel = null,
        ProfilePhotoBlobName = null,
        ProfileVerificationStatus = default,
        ProfileVerifiedAtUtc = null,
        ProfileVerifiedByUserKey = null,
        ProfileVerificationNote = null,
        PlastLevels = [],
        Offices = [],
        Assignments = [],
        Warnings = []
    };

    [Fact]
    public void ToLookupDto_FromASummary_ShouldSignThePhoto()
    {
        var summary = new MemberSummary(Guid.NewGuid(), Guid.NewGuid(), "Ірина", "Нова", "i@example.com", "s.jpg");

        var dto = summary.ToLookupDto(FakeBlobReadLinks.Instance);

        dto.MemberKey.Should().Be(summary.MemberKey);
        dto.UserKey.Should().Be(summary.UserKey);
        dto.MiddleName.Should().BeNull();
        dto.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("s.jpg"));
    }

}
