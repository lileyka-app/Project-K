using System.Reflection;
using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>
/// The person's card read from the record and the profile written onto it. The write is checked
/// field by field against everything the record has, so a field added to the record or the request
/// shows up here before it shows up in the base.
/// </summary>
public class MemberMappingTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] ProfileFields =
    [
        nameof(Member.FirstName), nameof(Member.MiddleName), nameof(Member.LastName), nameof(Member.Email),
        nameof(Member.PhoneNumber), nameof(Member.DateOfBirth), nameof(Member.Address), nameof(Member.School),
        nameof(Member.MotherFirstName), nameof(Member.MotherLastName), nameof(Member.MotherPhone),
        nameof(Member.FatherFirstName), nameof(Member.FatherLastName), nameof(Member.FatherPhone),
        nameof(Member.UpdatedDate)
    ];

    private static Member Stored() => new()
    {
        MemberKey = Guid.NewGuid(),
        PublicId = "PL-ABCDE-12345",
        UserKey = Guid.NewGuid(),
        FirstName = "Оксана",
        MiddleName = "Іванівна",
        LastName = "Тестова",
        Email = "old@example.com",
        PhoneNumber = "+380500000000",
        DateOfBirth = new DateOnly(2010, 5, 17),
        Address = "Old street",
        School = "Old school",
        MotherFirstName = "Мати",
        MotherLastName = "Стара",
        MotherPhone = "+380500000001",
        FatherFirstName = "Батько",
        FatherLastName = "Старий",
        FatherPhone = "+380500000002",
        CardExtras = """{"visa":"D"}""",
        ProfilePhotoBlobName = "photo.jpg",
        ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent,
        ProfileVerifiedAtUtc = Now.AddDays(-3),
        ProfileVerifiedByUserKey = Guid.NewGuid(),
        ProfileVerificationNote = "checked",
        LatestPlastLevel = PlastLevel.Uchasnyk,
        PlastLevelHistory = [new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Uchasnyk, DateAchieved = new DateOnly(2020, 1, 1) }],
        CreatedDate = Now.AddYears(-1),
        UpdatedDate = Now.AddDays(-1)
    };

    private static UpsertMemberProfileCommand Request() => new()
    {
        MemberKey = Guid.NewGuid(),
        KurinKey = Guid.NewGuid(),
        GroupKey = Guid.NewGuid(),
        FirstName = "Ірина",
        MiddleName = "Петрівна",
        LastName = "Нова",
        Email = "new@example.com",
        PhoneNumber = "+380509999999",
        DateOfBirth = new DateOnly(2011, 6, 18),
        Address = "New street",
        School = "New school",
        MotherFirstName = "Мама",
        MotherLastName = "Нова",
        MotherPhone = "+380509999991",
        FatherFirstName = "Тато",
        FatherLastName = "Новий",
        FatherPhone = "+380509999992",
        PlastLevelHistories = [new PlastLevelHistoryDto { PlastLevel = PlastLevel.Rozviduvach, DateAchieved = new DateOnly(2024, 1, 1) }],
        CardExtras = new Dictionary<string, string> { ["visa"] = "C" },
        ApplyCardFields = true
    };

    private static Dictionary<string, object?> Snapshot(Member member) => typeof(Member)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .ToDictionary(property => property.Name, property => property.GetValue(member));

    [Fact]
    public void ApplyTo_ShouldWriteTheProfileFields_AndNothingElse()
    {
        var member = Stored();
        var request = Request();
        var before = Snapshot(member);

        request.ApplyTo(member, Now);

        var after = Snapshot(member);
        var changed = after.Keys.Where(name => !Equals(before[name], after[name])).ToList();
        changed.Should().BeEquivalentTo(ProfileFields);

        member.FirstName.Should().Be(request.FirstName);
        member.MiddleName.Should().Be(request.MiddleName);
        member.LastName.Should().Be(request.LastName);
        member.Email.Should().Be(request.Email);
        member.PhoneNumber.Should().Be(request.PhoneNumber);
        member.DateOfBirth.Should().Be(request.DateOfBirth);
        member.Address.Should().Be(request.Address);
        member.School.Should().Be(request.School);
        member.ParentsDiffer(request).Should().BeFalse();
        member.UpdatedDate.Should().Be(Now);
    }

    [Fact]
    public void ApplyTo_ShouldLeaveTheKeyTheAccountTheLevelsAndTheExtrasToTheirOwners()
    {
        var member = Stored();
        var keyBefore = member.MemberKey;
        var levelsBefore = member.PlastLevelHistory.ToList();

        Request().ApplyTo(member, Now);

        member.MemberKey.Should().Be(keyBefore);
        member.PublicId.Should().Be("PL-ABCDE-12345");
        member.CardExtras.Should().Be("""{"visa":"D"}""");
        member.PlastLevelHistory.Should().Equal(levelsBefore);
        member.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);
    }

    [Fact]
    public void ToProfileCommand_ShouldCarryEveryProfileField_AndLeaveThePhotoAndTheAccount()
    {
        var command = new UpsertMemberCommand
        {
            MemberKey = Guid.NewGuid(),
            UserKey = Guid.NewGuid(),
            KurinKey = Guid.NewGuid(),
            GroupKey = Guid.NewGuid(),
            CreateUserAccount = true,
            FirstName = "Ірина",
            MiddleName = "Петрівна",
            LastName = "Нова",
            Email = "new@example.com",
            PhoneNumber = "+380509999999",
            DateOfBirth = new DateOnly(2011, 6, 18),
            Address = "New street",
            School = "New school",
            MotherFirstName = "Мама",
            MotherLastName = "Нова",
            MotherPhone = "+380509999991",
            FatherFirstName = "Тато",
            FatherLastName = "Новий",
            FatherPhone = "+380509999992",
            PlastLevelHistories = [new PlastLevelHistoryDto { PlastLevel = PlastLevel.Rozviduvach }],
            CardExtras = new Dictionary<string, string> { ["visa"] = "C" },
            ApplyCardFields = true,
            RemoveProfilePhoto = true,
            BlobFileName = "photo.jpg"
        };

        var profile = command.ToProfileCommand();

        // Every field of the profile write comes from the field of the same name on the form.
        foreach (var property in typeof(UpsertMemberProfileCommand).GetProperties())
        {
            var source = typeof(UpsertMemberCommand).GetProperty(property.Name);
            source.Should().NotBeNull($"the form carries {property.Name}");
            property.GetValue(profile).Should().BeEquivalentTo(source.GetValue(command), property.Name);
        }
    }

    [Fact]
    public void ToDto_ShouldReadTheCard_WithTheNewestLevelAndASignedPhoto()
    {
        var member = Stored();
        member.PlastLevelHistory.Add(new PlastLevelHistory { PlastLevel = PlastLevel.Rozviduvach, DateAchieved = new DateOnly(2024, 1, 1), IsPlastunka = true });
        member.MemberWarnings.Add(new MemberWarning { Level = MemberWarningLevel.Level1 });
        member.MemberAwards.Add(new MemberAward { Level = MemberAwardLevel.First, Status = BadgeProgressStatus.Submitted });

        var dto = member.ToDto(FakeBlobReadLinks.Instance, new Dictionary<Guid, GroupSummary>());

        dto.MemberKey.Should().Be(member.MemberKey);
        dto.PublicId.Should().Be(member.PublicId);
        dto.LatestPlastLevel.Should().Be(PlastLevel.Rozviduvach);
        dto.PlastLevelHistories.Should().HaveCount(2);
        dto.Warnings.Should().ContainSingle();
        dto.Awards.Should().ContainSingle().Which.ImageUrl.Should().Be("/api/awards/images/1?colored=False");
        dto.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("photo.jpg"));
        dto.CardExtras.Should().Contain("visa", "D");
        dto.MotherPhone.Should().Be(member.MotherPhone);
        // Where they stand and who reads them is filled by the read that knows it.
        dto.KurinKey.Should().BeEmpty();
        dto.UserRole.Should().BeNull();
        dto.HiddenData.Should().BeEmpty();
    }

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
        var summary = new MemberSummary(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ірина", "Нова", "i@example.com", "s.jpg");

        var dto = summary.ToLookupDto(FakeBlobReadLinks.Instance);

        dto.MemberKey.Should().Be(summary.MemberKey);
        dto.UserKey.Should().Be(summary.UserKey);
        dto.MiddleName.Should().BeNull();
        dto.ProfilePhotoUrl.Should().Be(FakeBlobReadLinks.Instance.For("s.jpg"));
    }

    [Fact]
    public void MaskFor_ShouldClearThePublicCode_ForAnyoneButItsOwner()
    {
        var member = Stored();
        var everything = MemberDataFieldRules.All.ToDictionary(info => info.Group, _ => CapabilityReach.Kurin);

        var own = member.ToDto(FakeBlobReadLinks.Instance, new Dictionary<Guid, GroupSummary>()).MaskFor(new MemberFieldMask(member.UserKey, everything, []), null);
        var other = member.ToDto(FakeBlobReadLinks.Instance, new Dictionary<Guid, GroupSummary>()).MaskFor(new MemberFieldMask(Guid.NewGuid(), everything, []), null);

        own.PublicId.Should().Be(member.PublicId);
        other.PublicId.Should().BeNull();
    }
}
