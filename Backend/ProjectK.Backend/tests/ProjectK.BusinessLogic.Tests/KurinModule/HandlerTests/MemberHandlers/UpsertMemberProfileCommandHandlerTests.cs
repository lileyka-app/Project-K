using ProjectK.Common.Phones;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class UpsertMemberProfileCommandHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock;
    private readonly Mock<IUnitOfWork> _kurinDataMock;
    private readonly Mock<IMemberRepository> _memberRepoMock;
    private readonly Mock<IGroupRepository> _groupRepoMock;
    private readonly Mock<IMembershipRepository> _membershipsMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IDomainEventPublisher> _eventsMock;
    private readonly Mock<IAccountProvisioningService> _accountsMock = new();
    private readonly Mock<IKraiProfileProvider> _profilesMock = new();
    private readonly UpsertMemberProfileCommandHandler _handler;
    private readonly IMapper _mapper;
    private readonly JournalRecorder _journal;

    public UpsertMemberProfileCommandHandlerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, loggerFactory);

        _uowMock = new Mock<IMemberUnitOfWork>();
        _kurinDataMock = new Mock<IUnitOfWork>();
        _memberRepoMock = new Mock<IMemberRepository>();
        _groupRepoMock = new Mock<IGroupRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _eventsMock = new Mock<IDomainEventPublisher>();
        _currentUserContextMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);
        _kurinDataMock.Setup(u => u.Groups).Returns(_groupRepoMock.Object);
        _kurinDataMock.Setup(u => u.Memberships).Returns(_membershipsMock.Object);
        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mapper = mapperConfig.CreateMapper();
        _journal = new JournalRecorder(_currentUserContextMock.Object);
        _handler = Handler(FieldAccess.SeesEverything());
    }

    private UpsertMemberProfileCommandHandler Handler(IMemberFieldAccess fieldAccess) => new(
        _uowMock.Object,
        _kurinDataMock.Object,
        _mapper,
        _currentUserContextMock.Object,
        _eventsMock.Object,
        _accountsMock.Object,
        _profilesMock.Object,
        fieldAccess,
        _journal.Audit);

    private void AccountIs(Guid userKey, OnboardingStatus status) =>
        _accountsMock.Setup(a => a.FindAsync(userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSnapshot(userKey, "old@example.com", status));

    private static Group MakeGroup(Guid? kurinKey = null)
    {
        var k = kurinKey ?? Guid.NewGuid();
        return new Group("G", k) { GroupKey = Guid.NewGuid(), Kurin = new Kurin(10) { KurinKey = k } };
    }

    private static Member MakeExistingMember(Guid? groupKey, Guid kurinKey) =>
        new()
        {
            MemberKey = Guid.NewGuid(),
            FirstName = "Old",
            MiddleName = "M",
            LastName = "Name",
            Email = "old@example.com",
            PhoneNumber = "+380501111111",
            DateOfBirth = new DateOnly(1990, 1, 1),
            ProfilePhotoBlobName = "old.png"
        };

    private void ExistingMemberIs(Member? member, Guid memberKey) =>
        _memberRepoMock.Setup(r => r.GetByKeyAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member!);

    /// <summary>
    /// Says where the person already stands. Nothing on their record answers that any more, so a
    /// fixture that omits this is describing someone who belongs nowhere.
    /// </summary>
    private void AlreadyPlacedIn(Guid memberKey, Guid kurinKey, Guid? groupKey) =>
        _membershipsMock
            .Setup(m => m.GetActiveForMemberAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Membership
            {
                MemberKey = memberKey,
                KurinKey = kurinKey,
                GroupKey = groupKey,
                JoinedAtUtc = DateTime.UtcNow.AddYears(-1)
            }]);

    private void GroupIs(Group group) =>
        _groupRepoMock.Setup(r => r.GetByKeyAsync(group.GroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

    [Fact]
    public async Task Handle_Create_NewMember_ShouldReportCreated()
    {
        var group = MakeGroup();
        var cmd = new UpsertMemberProfileCommand
        {
            GroupKey = group.GroupKey,
            FirstName = "Ivan",
            MiddleName = "I",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "+380501234567",
            DateOfBirth = new DateOnly(2001, 2, 3)
        };

        ExistingMemberIs(null, cmd.MemberKey);
        GroupIs(group);

        Member? created = null;
        _memberRepoMock.Setup(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((m, _) => created = m);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.IsCreated.Should().BeTrue();
        created.Should().NotBeNull();
        created!.FirstName.Should().Be("Ivan");
        created.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.Unverified);
        _memberRepoMock.Verify(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Once);

        // A new person is in no kurin yet: the entry goes to the one they are placed in. Their
        // contacts and birth date are recorded as given, never with what they are.
        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Member);
        entry.Action.Should().Be(AuditActions.Member.Added);
        entry.KurinKey.Should().Be(group.KurinKey);
        entry.SubjectKey.Should().Be(created.MemberKey);
        entry.SubjectMemberKey.Should().Be(created.MemberKey);
        entry.SubjectLabel.Should().Be("Ivan Petrenko");
        entry.Changes.Should().Equal(
            new AuditChange("firstName", null, "Ivan"),
            new AuditChange("middleName", null, "I"),
            new AuditChange("lastName", null, "Petrenko"),
            new AuditChange("email", null, null, Redacted: true),
            new AuditChange("phoneNumber", null, null, Redacted: true),
            new AuditChange("dateOfBirth", null, null, Redacted: true));
    }

    [Theory]
    [InlineData("+420 777 123 456", "+420777123456")]
    [InlineData(null, "")]
    public async Task Handle_Create_StoresThePhoneWithoutSeparators(string? sent, string stored)
    {
        var cmd = NewMemberInAGroup(sent!);
        var created = CaptureCreated();

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        created()!.PhoneNumber.Should().Be(stored);
    }

    private UpsertMemberProfileCommand NewMemberInAGroup(string phone)
    {
        var group = MakeGroup();
        var cmd = new UpsertMemberProfileCommand
        {
            GroupKey = group.GroupKey,
            FirstName = "Ivan",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = phone,
            DateOfBirth = new DateOnly(2001, 2, 3)
        };
        ExistingMemberIs(null, cmd.MemberKey);
        GroupIs(group);
        return cmd;
    }

    private Func<Member?> CaptureCreated()
    {
        Member? created = null;
        _memberRepoMock.Setup(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((m, _) => created = m);
        return () => created;
    }

    [Fact]
    public async Task Handle_Create_StoresParentsPhones_AndRefusesABadOne()
    {
        var cmd = NewMemberInAGroup("+380501234567");
        cmd.MotherFirstName = "Olena";
        cmd.MotherPhone = "+420 777 000 222";
        var created = CaptureCreated();

        (await _handler.Handle(cmd, CancellationToken.None)).Type.Should().Be(ResultType.Success);
        created()!.MotherFirstName.Should().Be("Olena");
        created()!.MotherPhone.Should().Be("+420777000222");

        cmd.FatherPhone = "0501234567";
        var refused = await _handler.Handle(cmd, CancellationToken.None);
        refused.ErrorCode.Should().Be(PhoneNumbers.ErrorCode);
    }

    [Fact]
    public async Task Handle_Create_RefusesAPhoneWithoutCountryCode()
    {
        var cmd = NewMemberInAGroup("0501234567");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be(PhoneNumbers.ErrorCode);
    }

    [Fact]
    public async Task Handle_FromTheForm_RefusesAMissingRequiredCardField()
    {
        var group = MakeGroup();
        _profilesMock.Setup(p => p.GetForKurinAsync(group.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KraiProfile("cz", "Чехія", 1, new KraiCatalogSettings(["cz"], []), new KraiLocaleSettings("CZK", "Europe/Prague", "uk-UA"),
                new KraiCardFieldsSettings(KraiCardFields.Configurable, [new KraiCardExtraField { Key = "visaType", Label = "Тип візи", Required = true }]), KraiDuesSettings.Default));
        var cmd = new UpsertMemberProfileCommand
        {
            GroupKey = group.GroupKey,
            FirstName = "Ivan",
            MiddleName = "I",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "+380501234567",
            DateOfBirth = new DateOnly(2001, 2, 3),
            ApplyCardFields = true
        };
        ExistingMemberIs(null, cmd.MemberKey);
        GroupIs(group);

        var refused = await _handler.Handle(cmd, CancellationToken.None);
        cmd.CardExtras = new Dictionary<string, string> { ["visaType"] = "D" };
        Member? created = null;
        _memberRepoMock.Setup(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((m, _) => created = m);
        var saved = await _handler.Handle(cmd, CancellationToken.None);

        refused.Type.Should().Be(ResultType.BadRequest);
        refused.ErrorCode.Should().Be(MemberCardFields.ErrorCode);
        saved.Type.Should().Be(ResultType.Success);
        created!.CardExtras.Should().Be("""{"visaType":"D"}""");
    }

    /// <summary>
    /// A person can be created with their ступені already known — the roster import does exactly
    /// that. The mapper leaves the collection alone by design, so until the create branch applied
    /// the history itself, every such person arrived with a blank ступінь and no dates. A live
    /// import is what surfaced it: four юнаки imported with their скоб and their заприсяження,
    /// and all four showed "—" in the registry.
    /// </summary>
    [Fact]
    public async Task Handle_Create_WithKnownLevels_ShouldRecordThemAndTheLatestOne()
    {
        var group = MakeGroup();
        _currentUserContextMock.SetupGet(x => x.Roles).Returns(new[] { "KV.Zvyazkovyi" });

        var cmd = new UpsertMemberProfileCommand
        {
            GroupKey = group.GroupKey,
            FirstName = "Ivan",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "+380501234567",
            DateOfBirth = new DateOnly(2010, 3, 12),
            PlastLevelHistories =
            [
                new PlastLevelHistoryDto { PlastLevel = PlastLevel.Uchasnyk, DateAchieved = new DateOnly(2022, 5, 1) },
                new PlastLevelHistoryDto { PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2024, 4, 22) }
            ]
        };

        ExistingMemberIs(null, cmd.MemberKey);
        GroupIs(group);

        Member? created = null;
        _memberRepoMock.Setup(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((m, _) => created = m);

        await _handler.Handle(cmd, CancellationToken.None);

        created.Should().NotBeNull();
        created!.PlastLevelHistory.Should().HaveCount(2);
        created.PlastLevelHistory.Select(entry => entry.PlastLevel)
            .Should().BeEquivalentTo([PlastLevel.Uchasnyk, PlastLevel.Skob]);
        created.LatestPlastLevel.Should().Be(PlastLevel.Skob);
    }

    [Fact]
    public async Task Handle_Create_KurinScopedMember_ShouldLeaveGroupNull()
    {
        var kurinKey = Guid.NewGuid();
        var cmd = new UpsertMemberProfileCommand
        {
            KurinKey = kurinKey,
            FirstName = "Kurin",
            LastName = "Member",
            Email = "kurin.member@example.com",
            PhoneNumber = "+380501234567",
            DateOfBirth = new DateOnly(2002, 2, 3)
        };

        ExistingMemberIs(null, cmd.MemberKey);

        Member? created = null;
        _memberRepoMock.Setup(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()))
            .Callback<Member, CancellationToken>((m, _) => created = m);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        created.Should().NotBeNull();
        // Placement is announced, not written on the record: what the kurin does with it is its own.
        _eventsMock.Verify(x => x.PublishAsync(
            It.Is<MemberPlaced>(e => e.KurinKey == kurinKey && e.GroupKey == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutGroupOrKurin_ShouldReturnNotFound()
    {
        var cmd = new UpsertMemberProfileCommand { FirstName = "Nowhere", Email = "nowhere@example.com" };

        ExistingMemberIs(null, cmd.MemberKey);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        _memberRepoMock.Verify(r => r.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Update_VerifiedCurrentMember_WhenProfileFieldChanges_ShouldMarkVerifiedStale()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        existing.ProfileVerifiedAtUtc = DateTime.UtcNow.AddDays(-1);
        existing.ProfileVerifiedByUserKey = Guid.NewGuid();

        var cmd = ProfileCommandFor(existing, group, firstName: "Changed");

        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.WasProfileVerifiedCurrent.Should().BeTrue();
        existing.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedStale);
        existing.ProfileVerifiedAtUtc.Should().NotBeNull();
        existing.ProfileVerifiedByUserKey.Should().NotBeNull();
    }

    /// <summary>The edit is measured against the card as it was before the request was mapped over it.</summary>
    [Fact]
    public async Task Handle_Update_RecordsOnlyWhatChanged_PrivateFieldsWithoutTheirValues()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.Address = "Стара 1";
        existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        var cmd = ProfileCommandFor(existing, group, firstName: "Changed", phone: "+380509999999");
        cmd.Address = "Нова 2";
        _journal.Knows(existing.MemberKey, group.KurinKey, group.GroupKey, "Old", "Name");

        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);

        await _handler.Handle(cmd, CancellationToken.None);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Member.Changed);
        entry.KurinKey.Should().Be(group.KurinKey);
        entry.SubjectMemberKey.Should().Be(existing.MemberKey);
        entry.SubjectGroupKey.Should().Be(group.GroupKey);
        entry.SubjectLabel.Should().Be("Changed Name");
        entry.Changes.Should().Equal(
            new AuditChange("firstName", "Old", "Changed"),
            new AuditChange("phoneNumber", null, null, Redacted: true),
            new AuditChange("address", null, null, Redacted: true),
            new AuditChange("profileVerification", "VerifiedCurrent", "VerifiedStale"));
    }

    [Fact]
    public async Task Handle_Update_VerifiedCurrentMember_WhenProfileDoesNotChange_ShouldKeepVerifiedCurrent()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;

        var cmd = ProfileCommandFor(existing, group);

        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);
        _journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Update_ShouldReportThePhotoItIsReplacing()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        var cmd = ProfileCommandFor(existing, group, firstName: "Changed");

        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Data!.PreviousPhotoBlobName.Should().Be("old.png");
        result.Data.IsCreated.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Update_LinkedMember_ByLeadership_ShouldPreserveEmail()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        var cmd = ProfileCommandFor(existing, group, firstName: "NewName", email: "different@example.com", phone: "+380502222222");

        _currentUserContextMock.Setup(x => x.IsInRole("KV.Vykhovnyk")).Returns(true);
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { "KV.Vykhovnyk" });
        AccountIs(existing.UserKey.Value, OnboardingStatus.Active);
        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.AccountEmailToFollow.Should().BeFalse();
        existing.FirstName.Should().Be("NewName");
        existing.Email.Should().Be("old@example.com");
        existing.PhoneNumber.Should().Be("+380502222222");
        _memberRepoMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Update_PendingAccount_ByLeadership_ShouldCorrectEmailAndAskTheAccountToFollow()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        var cmd = ProfileCommandFor(existing, group, email: "fixed@example.com");

        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { "KV.Vykhovnyk" });
        AccountIs(existing.UserKey.Value, OnboardingStatus.PendingActivation);
        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.AccountEmailToFollow.Should().BeTrue();
        existing.Email.Should().Be("fixed@example.com");
    }

    [Fact]
    public async Task Handle_Update_PendingAccount_WhenAddressIsTaken_ShouldRefuseAndWriteNothing()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        var cmd = ProfileCommandFor(existing, group, email: "someone.else@example.com");

        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { "KV.Vykhovnyk" });
        AccountIs(existing.UserKey.Value, OnboardingStatus.PendingActivation);
        _accountsMock.Setup(a => a.CheckAvailabilityAsync("someone.else@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountAvailability.EmailTaken);
        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("EmailTaken");
        existing.Email.Should().Be("old@example.com");
        _memberRepoMock.Verify(r => r.Update(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Update_LinkedMember_ByAdmin_ShouldAllowManualEmailChange()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        var cmd = ProfileCommandFor(existing, group, firstName: "NewName", email: "admin.changed@example.com", phone: "+380502222222");

        _currentUserContextMock.Setup(x => x.IsInRole("Admin")).Returns(true);
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { "Admin" });
        AccountIs(existing.UserKey.Value, OnboardingStatus.Active);
        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.AccountEmailToFollow.Should().BeTrue();
        existing.Email.Should().Be("admin.changed@example.com");
        existing.PhoneNumber.Should().Be("+380502222222");
    }

    [Fact]
    public async Task Handle_Update_LinkedMember_ByOwner_ShouldPreserveEmail()
    {
        var group = MakeGroup();
        var ownerUserKey = Guid.NewGuid();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = ownerUserKey;
        var cmd = ProfileCommandFor(existing, group, firstName: "SelfUpdated", email: "different@example.com", phone: "+380502222222");

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(ownerUserKey);
        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.FirstName.Should().Be("SelfUpdated");
        existing.Email.Should().Be("old@example.com");
        existing.PhoneNumber.Should().Be("+380502222222");
    }

    [Fact]
    public async Task Handle_Update_ByOwner_WhenLevelsChange_ShouldRecordThemAndMarkVerifiedStale()
    {
        var group = MakeGroup();
        var ownerUserKey = Guid.NewGuid();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = ownerUserKey;
        existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        var cmd = ProfileCommandFor(existing, group);
        cmd.PlastLevelHistories = [new PlastLevelHistoryDto { PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2024, 4, 22) }];

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(ownerUserKey);
        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.PlastLevelHistory.Select(entry => entry.PlastLevel).Should().BeEquivalentTo([PlastLevel.Skob]);
        existing.LatestPlastLevel.Should().Be(PlastLevel.Skob);
        existing.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedStale);
    }

    [Fact]
    public async Task Handle_Update_RecordsTheLevels_AsTheyWereAndAsTheyAre()
    {
        var group = MakeGroup();
        var ownerUserKey = Guid.NewGuid();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = ownerUserKey;
        var cmd = ProfileCommandFor(existing, group);
        cmd.PlastLevelHistories = [new PlastLevelHistoryDto { PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2024, 4, 22) }];
        _journal.Knows(existing.MemberKey, group.KurinKey, group.GroupKey, "Old", "Name");

        _currentUserContextMock.SetupGet(x => x.UserId).Returns(ownerUserKey);
        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);

        await _handler.Handle(cmd, CancellationToken.None);

        _journal.Entries.Single().Changes.Should().Equal(
            new AuditChange("plastLevels", null, $"{PlastLevelNames.Of(PlastLevel.Skob)} (2024-04-22)"));
    }

    [Fact]
    public async Task Handle_Update_ByRegularUserForAnotherMember_ShouldIgnoreLevels()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent;
        var cmd = ProfileCommandFor(existing, group);
        cmd.PlastLevelHistories = [new PlastLevelHistoryDto { PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2024, 4, 22) }];

        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.PlastLevelHistory.Should().BeEmpty();
        existing.ProfileVerificationStatus.Should().Be(MemberProfileVerificationStatus.VerifiedCurrent);
    }

    [Fact]
    public async Task Handle_Update_LinkedMember_ByRegularUserForAnotherMember_WhenContactInfoChanges_ShouldReturnBadRequest()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        var cmd = ProfileCommandFor(existing, group, firstName: "NewName", email: "different@example.com", phone: "+380502222222");

        ExistingMemberIs(existing, existing.MemberKey);
        GroupIs(group);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("ContactInfoLinked");
        existing.Email.Should().Be("old@example.com");
        existing.PhoneNumber.Should().Be("+380501111111");
        _memberRepoMock.Verify(r => r.Update(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SaveChangesFailed_ShouldReturnInternalServerError()
    {
        var group = MakeGroup();
        var cmd = new UpsertMemberProfileCommand { GroupKey = group.GroupKey, FirstName = "Ivan", Email = "ivan@example.com" };

        ExistingMemberIs(null, cmd.MemberKey);
        GroupIs(group);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.InternalServerError);
    }

    [Fact]
    public async Task Handle_Update_ShouldNotMoveThePersonEvenWhenLeadershipAsksItTo()
    {
        var here = MakeGroup();
        var elsewhere = MakeGroup();
        var existing = MakeExistingMember(here.GroupKey, here.KurinKey);

        // The form was opened from another гурток, so the request names it. Editing a profile is
        // not how someone is moved — the membership they already have has the last word.
        var cmd = ProfileCommandFor(existing, elsewhere, firstName: "Changed");

        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, here.KurinKey, here.GroupKey);
        GroupIs(here);
        GroupIs(elsewhere);
        _currentUserContextMock.Setup(c => c.Roles).Returns(new[] { "KV.Vykhovnyk" });

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _eventsMock.Verify(
            e => e.PublishAsync(
                It.Is<MemberPlaced>(placed =>
                    placed.KurinKey == here.KurinKey && placed.GroupKey == here.GroupKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static UpsertMemberProfileCommand ProfileCommandFor(
        Member existing,
        Group group,
        string? firstName = null,
        string? email = null,
        string? phone = null) => new()
        {
            MemberKey = existing.MemberKey,
            GroupKey = group.GroupKey,
            FirstName = firstName ?? existing.FirstName,
            MiddleName = existing.MiddleName ?? string.Empty,
            LastName = existing.LastName,
            Email = email ?? existing.Email,
            PhoneNumber = phone ?? existing.PhoneNumber,
            DateOfBirth = existing.DateOfBirth,
            Address = existing.Address,
            School = existing.School
        };

    /// <summary>
    /// The form of a writer who may not see the parents, the address, the birth date or the край's
    /// fields shows «Приховано» there and sends blanks back. Those blanks must not wipe what is
    /// stored, and a required field the writer never saw must not turn the save away.
    /// </summary>
    [Fact]
    public async Task Handle_FromTheForm_KeepsWhatTheWriterMayNotSee()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        existing.UserKey = Guid.NewGuid();
        existing.Address = "Stored St";
        existing.School = "Stored School";
        existing.MotherFirstName = "Olena";
        existing.MotherPhone = "+380501112233";
        existing.FatherPhone = "+380504445566";
        existing.CardExtras = """{"visaType":"D"}""";
        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);
        _profilesMock.Setup(p => p.GetForKurinAsync(group.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KraiProfile("cz", "Чехія", 1, new KraiCatalogSettings(["cz"], []), new KraiLocaleSettings("CZK", "Europe/Prague", "uk-UA"),
                new KraiCardFieldsSettings(
                    new Dictionary<string, string> { [KraiCardFields.Parents] = KraiCardFields.Required, [KraiCardFields.Address] = KraiCardFields.Required },
                    [new KraiCardExtraField { Key = "visaType", Label = "Тип візи", Required = true }, new KraiCardExtraField { Key = "school2", Label = "Друга школа", Required = true }]),
                KraiDuesSettings.Default));
        var writer = new MemberFieldMask(
            Guid.NewGuid(),
            new Dictionary<MemberDataGroup, CapabilityReach> { [MemberDataGroup.Contacts] = CapabilityReach.Kurin },
            []);
        var cmd = new UpsertMemberProfileCommand
        {
            MemberKey = existing.MemberKey,
            FirstName = "New",
            MiddleName = "M",
            LastName = "Name",
            Email = existing.Email,
            PhoneNumber = existing.PhoneNumber,
            DateOfBirth = default,
            Address = string.Empty,
            School = string.Empty,
            MotherFirstName = string.Empty,
            MotherLastName = string.Empty,
            MotherPhone = string.Empty,
            FatherFirstName = string.Empty,
            FatherLastName = string.Empty,
            FatherPhone = string.Empty,
            CardExtras = new Dictionary<string, string> { ["visaType"] = string.Empty },
            ApplyCardFields = true
        };

        var result = await Handler(FieldAccess.Of(writer)).Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.FirstName.Should().Be("New");
        existing.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        existing.Address.Should().Be("Stored St");
        existing.School.Should().Be("Stored School");
        existing.MotherFirstName.Should().Be("Olena");
        existing.MotherPhone.Should().Be("+380501112233");
        existing.FatherPhone.Should().Be("+380504445566");
        existing.CardExtras.Should().Be("""{"visaType":"D"}""");
    }

    [Fact]
    public async Task Handle_KeepsTheContacts_OfAWriterWhoMayNotSeeThem()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);
        var cmd = new UpsertMemberProfileCommand
        {
            MemberKey = existing.MemberKey,
            FirstName = existing.FirstName,
            MiddleName = "M",
            LastName = existing.LastName,
            Email = string.Empty,
            PhoneNumber = string.Empty,
            DateOfBirth = existing.DateOfBirth
        };

        var result = await Handler(FieldAccess.SeesNothing(Guid.NewGuid())).Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.Email.Should().Be("old@example.com");
        existing.PhoneNumber.Should().Be("+380501111111");
    }

    [Fact]
    public async Task Handle_RefusesAnEmptyEmail_FromAWriterWhoSeesTheContacts()
    {
        var group = MakeGroup();
        var existing = MakeExistingMember(group.GroupKey, group.KurinKey);
        ExistingMemberIs(existing, existing.MemberKey);
        AlreadyPlacedIn(existing.MemberKey, group.KurinKey, group.GroupKey);
        GroupIs(group);
        var cmd = new UpsertMemberProfileCommand
        {
            MemberKey = existing.MemberKey,
            FirstName = existing.FirstName,
            MiddleName = "M",
            LastName = existing.LastName,
            Email = string.Empty,
            PhoneNumber = existing.PhoneNumber,
            DateOfBirth = existing.DateOfBirth
        };

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be(UpsertMemberProfileCommandHandler.EmailRequired);
        existing.Email.Should().Be("old@example.com");
    }
}
