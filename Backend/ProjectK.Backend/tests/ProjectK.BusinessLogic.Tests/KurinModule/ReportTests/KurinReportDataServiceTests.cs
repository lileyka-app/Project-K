using ProjectK.BusinessLogic.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.ReportTests;

/// <summary>
/// What the звіт куреня says about the склад. Every assertion here has a twin on the реєстр side —
/// the two are read side by side by the same person, and this is where they are kept from drifting.
/// </summary>
public sealed class KurinReportDataServiceTests
{
    private readonly Mock<IKurinReportSource> _source = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IKurinReportMedia> _media = new();
    private readonly Mock<IMemberProgressDirectory> _progress = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IKurinTagRepository> _tags = new();
    private readonly Mock<IKurinTagVisibility> _tagVisibility = new();
    private readonly KurinReportDataService _service;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _alphaKey = Guid.NewGuid();
    private readonly Guid _betaKey = Guid.NewGuid();
    private readonly Kurin _kurin;
    private readonly Group _alpha;
    private readonly Group _beta;

    private readonly List<Member> _members = [];
    private readonly List<Membership> _memberships = [];
    private readonly List<MentorAssignment> _mentorAssignments = [];

    public KurinReportDataServiceTests()
    {
        _kurin = new Kurin(7) { KurinKey = _kurinKey };
        _alpha = new Group("Alpha", _kurinKey) { GroupKey = _alphaKey };
        _beta = new Group("Beta", _kurinKey) { GroupKey = _betaKey };

        _media.Setup(m => m.TryDownloadAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Nobody here has taken a проба; the report asks the module and is told so.
        _progress
            .Setup(p => p.GetDetailsForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, MemberProgressDetail>());

        _uow.Setup(u => u.KurinTags).Returns(_tags.Object);
        TagsOn(false);

        _service = new KurinReportDataService(
            _source.Object,
            _currentUser.Object,
            FakeBlobReadLinks.Instance,
            _media.Object,
            Mock.Of<IProbesCatalogService>(),
            Mock.Of<IBadgesCatalogService>(),
            new ConfigurationBuilder().Build(),
            _progress.Object,
            _uow.Object,
            _tagVisibility.Object,
            Mock.Of<ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule.IHonoursCatalog>());
    }

    private void TagsOn(bool enabled, Func<Guid?, Guid?, bool>? shows = null)
    {
        _tagVisibility
            .Setup(v => v.SettingsAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KurinTagSettingsState(enabled, EnabledByDefault: false, OwnTagsVisible: true));
        _tagVisibility
            .Setup(v => v.ShowsTagsOfAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shows ?? ((_, _) => enabled));
    }

    private (Member Alpha, Member Beta) TaggedInBothGroups()
    {
        var alpha = Person("Альфа", _alphaKey);
        var beta = Person("Бета", _betaKey);
        var tag = new KurinTag { KurinKey = _kurinKey, Name = "Новики" };
        _tags.Setup(t => t.GetForKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([tag]);
        _tags.Setup(t => t.GetAssignmentsAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberTag { KurinTagKey = tag.KurinTagKey, MemberKey = alpha.MemberKey },
            new MemberTag { KurinTagKey = tag.KurinTagKey, MemberKey = beta.MemberKey }
        ]);
        return (alpha, beta);
    }

    /// <summary>The report names tags by the reader's own «Бачити теги людей»: a гурток's reach shows that гурток only.</summary>
    [Fact]
    public async Task ShouldNameOnlyTheTagsTheReaderMaySee()
    {
        TaggedInBothGroups();
        TagsOn(true, (_, groupKey) => groupKey == _alphaKey);

        var report = await Build();

        report!.ShowsTags.Should().BeTrue();
        report.Youth.Single(member => member.FullName == "Тест Альфа").Tags.Should().Equal("Новики");
        report.Youth.Single(member => member.FullName == "Тест Бета").Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldCarryNoTagsAtAll_ForAReaderTheKurinShowsNone()
    {
        TaggedInBothGroups();
        TagsOn(true, (_, _) => false);

        var report = await Build();

        report!.ShowsTags.Should().BeFalse();
        report.Members.Should().OnlyContain(member => member.Tags.Count == 0);
    }

    private Member Person(string lastName, Guid? groupKey = null, PlastLevel? level = null, bool withAccount = false)
    {
        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            UserKey = withAccount ? Guid.NewGuid() : null,
            FirstName = "Тест",
            MiddleName = null,
            LastName = lastName,
            Email = $"{lastName}@example.com",
            PhoneNumber = "0500000000",
            DateOfBirth = new DateOnly(2008, 1, 1),
            LatestPlastLevel = level
        };

        _members.Add(member);
        _memberships.Add(new Membership
        {
            MemberKey = member.MemberKey,
            KurinKey = _kurinKey,
            GroupKey = groupKey,
            JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
        });

        return member;
    }

    private void GiveOffice(
        Member member,
        LeadershipType type,
        LeadershipRole role,
        Guid? groupKey = null,
        DateOnly? heldUntil = null,
        Guid? inKurinKey = null)
    {
        var office = new Leadership
        {
            LeadershipKey = Guid.NewGuid(),
            Type = type,
            KurinKey = type == LeadershipType.Group ? null : inKurinKey ?? _kurinKey,
            GroupKey = groupKey,
            // Без власної назви — так їх і заводять UpsertLeadershipCommand і сідери. Назва, якщо є,
            // перебиває все інше, і фікстура з нею не перевіряла б нічого.
            Name = null,
            StartDate = new DateOnly(2024, 1, 1)
        };

        member.LeadershipHistories.Add(new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            MemberKey = member.MemberKey,
            LeadershipKey = office.LeadershipKey,
            Leadership = office,
            Role = role,
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = heldUntil
        });
    }

    private void MentorOf(Member member, Guid groupKey, DateTime? revokedAtUtc = null)
        => _mentorAssignments.Add(new MentorAssignment
        {
            MentorUserKey = member.UserKey!.Value,
            GroupKey = groupKey,
            AssignedAtUtc = DateTime.UtcNow.AddYears(-1),
            RevokedAtUtc = revokedAtUtc
        });

    private Task<Common.Models.Reports.KurinReportData?> Build(KurinBranch branch = KurinBranch.UPYu)
    {
        _kurin.SetBranches([new KurinNumbering(branch, 7)]);
        _source
            .Setup(s => s.LoadAsync(_kurinKey, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KurinReportSourceData(
                _kurin,
                [_alpha, _beta],
                _mentorAssignments,
                _members,
                new Dictionary<Guid, AppUser>(),
                _memberships.ToDictionary(membership => membership.MemberKey)));

        return _service.BuildAsync(_kurinKey, CancellationToken.None);
    }

    /// <summary>
    /// The report is read only by whoever holds the whole kurin, so it names every tag a person
    /// carries — by name, in order — and says it has tags at all only when the kurin uses them.
    /// </summary>
    [Fact]
    public async Task ShouldNameEveryTag_WhenTheKurinUsesTags()
    {
        var tagged = Person("Юнак", _alphaKey);
        Person("Без тегів", _alphaKey);
        var camp = new KurinTag { KurinKey = _kurinKey, Name = "Табір" };
        var novice = new KurinTag { KurinKey = _kurinKey, Name = "Новики" };
        TagsOn(true);
        _tags.Setup(t => t.GetForKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([camp, novice]);
        _tags.Setup(t => t.GetAssignmentsAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberTag { KurinTagKey = camp.KurinTagKey, MemberKey = tagged.MemberKey },
            new MemberTag { KurinTagKey = novice.KurinTagKey, MemberKey = tagged.MemberKey }
        ]);

        var report = await Build();

        report!.ShowsTags.Should().BeTrue();
        report.Youth.Single(member => member.FullName == "Тест Юнак").Tags.Should().Equal("Новики", "Табір");
        report.Youth.Single(member => member.FullName == "Тест Без тегів").Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldLeaveTagsOut_WhenTheKurinHasThemOff()
    {
        Person("Юнак", _alphaKey);

        var report = await Build();

        report!.ShowsTags.Should().BeFalse();
        report.Members.Should().OnlyContain(member => member.Tags.Count == 0);
        _tags.Verify(t => t.GetAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The same line the реєстр draws: only a КВ office is кадра. It used to be «a Kurin or KV
    /// office, or any global Identity role granting group leadership» — which put the курінний
    /// among the впорядники, and anyone holding a виховник office in a *different* kurin too.
    /// </summary>
    [Fact]
    public async Task ShouldCallOnlyKvOfficersStaff()
    {
        Person("Юнак", _alphaKey);
        var hurtkovyi = Person("Гурткова", _alphaKey);
        var kurinnyi = Person("Курінна", _alphaKey);
        var mentor = Person("Виховна", withAccount: true);
        GiveOffice(hurtkovyi, LeadershipType.Group, LeadershipRole.Hurtkoviy, _alphaKey);
        GiveOffice(kurinnyi, LeadershipType.Kurin, LeadershipRole.Kurinnuy);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);

        var report = await Build();

        report!.Staff.Select(member => member.FullName).Should().Equal("Тест Виховна");
        report.Youth.Select(member => member.FullName)
            .Should().BeEquivalentTo("Тест Юнак", "Тест Гурткова", "Тест Курінна");
    }

    /// <summary>
    /// The реєстр and the sidebar call someone закріплений за гуртком a впорядник even with no КВ
    /// office; the звіт used to put that same person among the юнаки. A revoked assignment counts
    /// for nothing, and so does one in another kurin's гурток.
    /// </summary>
    [Fact]
    public async Task ShouldCallSomeoneStaffOnAnAssignmentAlone_AsTheРеєстрDoes()
    {
        var assigned = Person("Закріплена", withAccount: true);
        MentorOf(assigned, _alphaKey);
        var revoked = Person("Знята", withAccount: true);
        MentorOf(revoked, _betaKey, revokedAtUtc: DateTime.UtcNow.AddDays(-1));
        var elsewhere = Person("Чужа", withAccount: true);
        MentorOf(elsewhere, Guid.NewGuid());

        var report = await Build();

        report!.Staff.Select(member => member.FullName).Should().Equal("Тест Закріплена");
        report.Youth.Select(member => member.FullName).Should().BeEquivalentTo("Тест Знята", "Тест Чужа");
    }

    [Fact]
    public async Task ShouldNotCallSomeoneStaffOnceTheirTermHasEnded()
    {
        var former = Person("Колишня", withAccount: true);
        GiveOffice(former, LeadershipType.KV, LeadershipRole.Vykhovnyk, heldUntil: new DateOnly(2025, 6, 1));

        var report = await Build();

        report!.Staff.Should().BeEmpty();
        report.Youth.Should().ContainSingle();
    }

    [Fact]
    public async Task ShouldNameTheGroupsEachStaffMemberIsAssignedTo()
    {
        var mentor = Person("Виховна", withAccount: true);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);
        MentorOf(mentor, _betaKey);
        MentorOf(mentor, _alphaKey);
        MentorOf(mentor, _alphaKey, revokedAtUtc: DateTime.UtcNow.AddDays(-1));

        var report = await Build();

        report!.Staff.Single().MentoredGroupNames.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public async Task Tally_ShouldCountYouthByLevel_AndLeaveStaffOut()
    {
        Person("Один", _alphaKey, PlastLevel.Uchasnyk);
        Person("Два", _alphaKey, PlastLevel.Uchasnyk);
        Person("Три", _alphaKey, PlastLevel.Skob);
        var mentor = Person("Виховна", level: PlastLevel.Uchasnyk, withAccount: true);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);

        var report = await Build();
        var counts = report!.LevelTally.ToDictionary(row => row.Label, row => row.Count);

        counts["пл. уч."].Should().Be(2, "the виховник is кадра, not юнацтво");
        counts["пл. скоб / вірл."].Should().Be(1);
        counts["Разом"].Should().Be(3);
    }

    [Fact]
    public async Task Tally_ShouldNotSwallowPeopleWithNoLevel()
    {
        Person("Без", _alphaKey);
        Person("Учасник", _alphaKey, PlastLevel.Uchasnyk);

        var report = await Build();
        var counts = report!.LevelTally.ToDictionary(row => row.Label, row => row.Count);

        counts["Без ступеня / інший"].Should().Be(1);
        counts["Разом"].Should().Be(2);
    }

    [Fact]
    public async Task Tally_ShouldFollowTheKurinsBranch()
    {
        Person("Сеньйор", _alphaKey, PlastLevel.SeniorPratsi);

        var report = await Build(KurinBranch.UPS);
        var counts = report!.LevelTally.ToDictionary(row => row.Label, row => row.Count);

        counts.Should().ContainKey("пл. сен. праці");
        counts["пл. сен. праці"].Should().Be(1);
        counts.Should().NotContainKey("Без ступеня / інший");
    }

    /// <summary>
    /// Offices are read along with the person, and from 0.20 a person can be in several kurins. The
    /// звіт is about one of them, so an office held somewhere else has no business being printed
    /// here — nor counting its holder as this kurin's кадра.
    /// </summary>
    [Fact]
    public async Task ShouldIgnoreOfficesHeldInAnotherKurin()
    {
        var visitor = Person("Гостя", _alphaKey, withAccount: true);
        GiveOffice(visitor, LeadershipType.KV, LeadershipRole.Vykhovnyk, inKurinKey: Guid.NewGuid());

        var report = await Build();

        report!.Staff.Should().BeEmpty("the КВ office belongs to a different kurin");
        report.Members.Single().LeadershipHistory.Should().BeEmpty();
    }

    /// <summary>
    /// The звіт names offices the way a провід writes them. The Впорядники table used to print the
    /// identity roles instead — «KV.Vykhovnyk, Member», the very string inside the token.
    /// </summary>
    [Fact]
    public async Task ShouldNameOfficesInWords()
    {
        var mentor = Person("Виховна", withAccount: true);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);

        var report = await Build();

        report!.Staff.Single().LeadershipHistory.Single().RoleLabel.Should().Be("Впорядник");
    }

    /// <summary>A senior kurin's own names for its roles, as on its screens: its Писар is «Скриптор» in the звіт too.</summary>
    [Fact]
    public async Task ShouldNameOfficesByTheKurinsOwnRoleNames()
    {
        var scribe = Person("Федорів", withAccount: true);
        GiveOffice(scribe, LeadershipType.KV, LeadershipRole.Pysar);
        var mentor = Person("Виховна", withAccount: true);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);
        _kurin.RoleNames = """{"Pysar":"Скриптор"}""";

        var report = await Build(KurinBranch.USP);

        report!.Staff.SelectMany(member => member.LeadershipHistory).Select(history => history.RoleLabel)
            .Should().BeEquivalentTo("Скриптор", "Впорядник");
    }

    /// <summary>
    /// A гуртковий's office is scoped to a гурток and says which. A курінний's and a виховник's are
    /// scoped to the kurin the звіт is already about, so they say nothing — they used to print the
    /// literals "Kurin" and "KV", which read as «КВ/Впорядник; KV» in the document.
    /// </summary>
    [Fact]
    public async Task ShouldScopeAnOfficeByItsГурток_AndLeaveKurinWideOnesUnqualified()
    {
        var hurtkovyi = Person("Гурткова", _alphaKey);
        var mentor = Person("Виховна", withAccount: true);
        GiveOffice(hurtkovyi, LeadershipType.Group, LeadershipRole.Hurtkoviy, _alphaKey);
        GiveOffice(mentor, LeadershipType.KV, LeadershipRole.Vykhovnyk);

        var report = await Build();

        report!.Youth.Single(member => member.FullName == "Тест Гурткова")
            .LeadershipHistory.Single().ScopeName.Should().Be("Alpha");
        report.Staff.Single().LeadershipHistory.Single().ScopeName.Should().BeNull();
    }

    /// <summary>
    /// The реєстр reads the newest recorded ступінь and falls back to the stored field. The report
    /// used to read the stored field alone, so a ступінь added with an earlier date — which is how
    /// the roster import writes them — showed one thing on screen and another in the PDF.
    /// </summary>
    [Fact]
    public async Task ShouldReadTheNewestRecordedLevel_NotTheStoredOne()
    {
        var member = Person("Просунулась", _alphaKey, PlastLevel.Uchasnyk);
        member.PlastLevelHistory.Add(new PlastLevelHistory
        {
            MemberKey = member.MemberKey,
            PlastLevel = PlastLevel.Rozviduvach,
            DateAchieved = new DateOnly(2026, 3, 1)
        });

        var report = await Build();

        report!.Youth.Single().LatestPlastLevel.Should().Be(PlastLevel.Rozviduvach);
        report.LevelTally.Single(row => row.Label == "пл. розв.").Count.Should().Be(1);
    }
}
