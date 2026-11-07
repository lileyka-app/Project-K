using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Account;
using MediatR;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.ImportHandlers;

/// <summary>
/// The import is exercised through a dry run, which is the same code path that writes — so what
/// these assert about a row is what would actually happen to it.
/// </summary>
public class ImportRosterHandlerTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();
    private static readonly Guid GroupKey = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _kurinData = new();
    private readonly Mock<IMemberUnitOfWork> _memberData = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly ImportRosterCommandHandler _handler;

    private List<MemberIdentity> _knownPeople = [];
    private List<MemberSummary> _peopleHere = [];

    public ImportRosterHandlerTests()
    {
        _kurins.Setup(r => r.GetByKeyAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(2) { KurinKey = KurinKey });
        _groups.Setup(r => r.GetAllAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Group> { new("Ведмеді", KurinKey) { GroupKey = GroupKey } });
        _members.Setup(r => r.FindPossibleMatchesAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _knownPeople);
        _members.Setup(r => r.GetSummariesByKurinKeyAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _peopleHere);

        _kurinData.Setup(u => u.Kurins).Returns(_kurins.Object);
        _kurinData.Setup(u => u.Groups).Returns(_groups.Object);
        _kurinData.Setup(u => u.Memberships).Returns(_memberships.Object);
        _memberData.Setup(u => u.Members).Returns(_members.Object);

        _handler = new ImportRosterCommandHandler(_kurinData.Object, _memberData.Object, _mediator.Object);
    }

    /// <summary>Прізвище, Ім'я, Дата народження, Ступінь, Дата ступеня, Гурток, Курінь, Пошта.</summary>
    private static readonly IReadOnlyList<ColumnMapping> FullMapping =
    [
        new(0, RosterField.LastName),
        new(1, RosterField.FirstName),
        new(2, RosterField.DateOfBirth),
        new(3, RosterField.PlastLevel),
        new(4, RosterField.PlastLevelDate),
        new(5, RosterField.GroupName),
        new(6, RosterField.KurinNumber),
        new(7, RosterField.Email)
    ];

    private static SheetRow Row(int number, params string[] cells) => new(number, cells);


    /// <summary>
    /// Every imported person gets an account: the file is how a kurin brings its people in, and
    /// someone with no way to sign in is only half brought in. The dry run promises it, the real
    /// run does it, and a row without an address is rejected and says why.
    /// </summary>
    [Fact]
    public async Task ASeniorKurin_IsRefused_AndGetsNoGroups()
    {
        _kurins.Setup(r => r.GetByKeyAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(5, KurinBranch.USP) { KurinKey = KurinKey });

        var report = await _handler.Handle(
            new ImportRosterCommand(KurinKey, [Row(2, "Петренко", "Іван", "01.01.1990", "", "", "Ведмеді", "5", "ivan@example.com")], FullMapping, CreateMissingGroups: true, DryRun: false),
            CancellationToken.None);

        report.ErrorCode.Should().Be(ImportRosterCommandHandler.NotForSeniorKurinCode);
        _groups.Verify(g => g.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARowWithAnAddress_IsPromisedAnAccount_OnADryRun_AndOneWithoutIsRejected()
    {
        var report = await _handler.Handle(
            new ImportRosterCommand(
                KurinKey,
                [
                    Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com"),
                    Row(3, "Коваль", "Марта", "02.02.2011", "скоб", "22.04.2024", "Ведмеді", "2", "")
                ],
                FullMapping,
                CreateMissingGroups: false,
                DryRun: true),
            CancellationToken.None);

        report.Data!.InvitedCount.Should().Be(1);
        report.Data.Rows.Single(row => row.RowNumber == 2).AccountInvited.Should().BeTrue();
        var without = report.Data.Rows.Single(row => row.RowNumber == 3);
        without.Outcome.Should().Be(RowOutcome.Rejected);
        without.AccountInvited.Should().BeFalse();
        without.Reason.Should().Contain("пошти");
        _mediator.Verify(m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AConfirmedImport_OpensAnAccount_ForTheRowWithAnAddress()
    {
        var memberKey = Guid.NewGuid();
        _mediator
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(memberKey, true, false, null)));
        _mediator
            .Setup(m => m.Send(It.Is<ProvisionMemberAccountCommand>(c => c.MemberKey == memberKey), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberInvitation>(ResultType.Success, new MemberInvitation(Guid.NewGuid(), true)));

        var report = await _handler.Handle(
            new ImportRosterCommand(
                KurinKey,
                [Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")],
                FullMapping,
                CreateMissingGroups: false,
                DryRun: false),
            CancellationToken.None);

        report.Data!.CreatedCount.Should().Be(1);
        report.Data.InvitedCount.Should().Be(1);
        _mediator.Verify(m => m.Send(It.Is<ProvisionMemberAccountCommand>(c => c.MemberKey == memberKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AConfirmedImport_NotesWhenTheAccountCouldNotBeOpened_ButKeepsThePerson()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(Guid.NewGuid(), true, false, null)));
        _mediator
            .Setup(m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberInvitation>(ResultType.InternalServerError));

        var report = await _handler.Handle(
            new ImportRosterCommand(
                KurinKey,
                [Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")],
                FullMapping,
                CreateMissingGroups: false,
                DryRun: false),
            CancellationToken.None);

        var row = report.Data!.Rows.Single();
        row.Outcome.Should().Be(RowOutcome.Created);
        row.AccountInvited.Should().BeFalse();
        row.Reason.Should().Contain("акаунт");
    }

    private Task<ServiceResult<RosterImportReport>> RunAsync(
        IReadOnlyList<SheetRow> rows,
        bool createMissingGroups = false)
        => _handler.Handle(
            new ImportRosterCommand(KurinKey, rows, FullMapping, createMissingGroups, DryRun: true),
            CancellationToken.None);

    [Fact]
    public async Task ARowWithEverything_IsAPersonToCreate()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
        report.Data.RejectedCount.Should().Be(0);
    }

    /// <summary>
    /// The rule from IMPORT-01: a ступінь is an event, and without the day it happened it is not
    /// a fact the system will store. The row is rejected rather than half-imported.
    /// </summary>
    [Fact]
    public async Task ALevelWithoutItsDate_IsRejected()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "", "Ведмеді", "2", "ivan@example.com")]);

        var row = report.Data!.Rows.Single();
        row.Outcome.Should().Be(RowOutcome.Rejected);
        row.Reason.Should().Contain("дати");
    }

    [Fact]
    public async Task ADateWithoutALevel_IsRejectedToo()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.Rows.Single().Outcome.Should().Be(RowOutcome.Rejected);
    }

    [Theory]
    [InlineData("Прізвище відсутнє", "", "Іван")]
    [InlineData("Ім'я відсутнє", "Петренко", "")]
    public async Task ARowWithoutAName_IsRejected(string _, string lastName, string firstName)
    {
        var report = await RunAsync([Row(2, lastName, firstName, "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.Rows.Single().Outcome.Should().Be(RowOutcome.Rejected);
    }

    [Fact]
    public async Task AGurtokTheKurinDoesNotHave_IsRejectedUnlessOpeningItWasAsked()
    {
        var rows = new[] { Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Соколи", "2", "ivan@example.com") };

        var refused = await RunAsync(rows);
        refused.Data!.Rows.Single().Outcome.Should().Be(RowOutcome.Rejected);
        refused.Data.MissingGroups.Should().ContainSingle().Which.Should().Be("Соколи");

        var allowed = await RunAsync(rows, createMissingGroups: true);
        allowed.Data!.Rows.Single().Outcome.Should().Be(RowOutcome.Created);
    }

    /// <summary>
    /// A person the system already knows is taken into the kurin, not opened a second time. This
    /// is what the person-centric model is for, and getting it wrong is how a roster becomes two
    /// of everyone.
    /// </summary>
    [Fact]
    public async Task SomeoneAlreadyInTheSystem_IsAttachedRatherThanDuplicated()
    {
        _knownPeople =
        [
            new MemberIdentity(Guid.NewGuid(), "Іван", "Петренко", "ivan@example.com", "0501112233", new DateOnly(2010, 1, 1))
        ];

        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.AttachedCount.Should().Be(1);
        report.Data.CreatedCount.Should().Be(0);
    }

    /// <summary>A name alone is not a match — one станиця can hold two Іван Петренки.</summary>
    [Fact]
    public async Task TheSameNameOnADifferentBirthday_IsADifferentPerson()
    {
        _knownPeople =
        [
            new MemberIdentity(Guid.NewGuid(), "Іван", "Петренко", "ivan@example.com", "0501112233", new DateOnly(2005, 6, 6))
        ];

        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan.jr@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task SomeoneAlreadyInThisKurin_IsLeftAlone()
    {
        var memberKey = Guid.NewGuid();
        _knownPeople =
        [
            new MemberIdentity(memberKey, "Іван", "Петренко", "ivan@example.com", "0501112233", new DateOnly(2010, 1, 1))
        ];
        _peopleHere =
        [
            new MemberSummary(memberKey, null, KurinKey, GroupKey, "Іван", "Петренко", "ivan@example.com", null)
        ];

        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.AlreadyHereCount.Should().Be(1);
    }

    /// <summary>
    /// A roster written for another kurin still imports — the провід is warned, not stopped.
    /// Reports name the kurin in words as often as in numbers, and refusing on that would refuse
    /// most real files.
    /// </summary>
    /// <summary>
    /// The import lands in the kurin it was started from, so the file need not say which one;
    /// a blank kurin cell is neither a rejection nor a foreign-kurin warning.
    /// </summary>
    [Fact]
    public async Task ARowWithoutAKurin_IsImportedHere_WithoutAWarning()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "", "ivan@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
        report.Data.RejectedCount.Should().Be(0);
        report.Data.ForeignKurinRows.Should().BeEmpty();
    }

    [Fact]
    public async Task ARowNamingAnotherKurin_IsFlaggedButNotRefused()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "14", "ivan@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
        report.Data.ForeignKurinRows.Should().ContainSingle().Which.Should().Be(2);
    }

    /// <summary>Dates arrive in whatever the file used, including Excel's own day count.</summary>
    [Theory]
    [InlineData("2010-01-01")]
    [InlineData("01.01.2010")]
    [InlineData("1/1/2010")]
    [InlineData("40179")]
    public async Task ABirthdayIsRead_InEveryShapeARosterUses(string birthday)
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", birthday, "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task ADateNobodyCouldRead_IsARejectionAndNotAGuess()
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "торік", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.Rows.Single().Outcome.Should().Be(RowOutcome.Rejected);
    }

    /// <summary>
    /// Rosters say the ступінь in whatever words the person writing had to hand — and the
    /// likeliest roster of all is one pasted out of this app, which prints the short forms.
    /// «пл. уч.» and «пл. гетьм. скоб» were both unrecognised until a live import said so.
    /// </summary>
    [Theory]
    [InlineData("скоб")]
    [InlineData("Вірлиця")]
    [InlineData("розвідувачка")]
    [InlineData("учасник")]
    [InlineData("прихильниця")]
    [InlineData("пл. прих.")]
    [InlineData("пл. уч.")]
    [InlineData("пл. розв.")]
    [InlineData("пл. скоб")]
    [InlineData("пл. гетьм. скоб")]
    [InlineData("Гетьманський скоб")]
    [InlineData("ст. пл.")]
    [InlineData("пл. сен. пр.")]
    [InlineData("пл. сен. дов.")]
    [InlineData("пл. сен. кер.")]
    public async Task ALevelIsRecognised_InTheWordsRostersUse(string level)
    {
        var report = await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", level, "22.04.2024", "Ведмеді", "2", "ivan@example.com")]);

        report.Data!.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task ADryRun_WritesNothing()
    {
        await RunAsync([Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Соколи", "2", "ivan@example.com")], createMissingGroups: true);

        _mediator.Verify(
            m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(
            m => m.Send(It.IsAny<JoinKurinCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _groups.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
        _kurinData.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The other half of the dry run: once confirmed, the гурток is opened and the person is
    /// written. Without this, "a dry run writes nothing" would also pass on an import that never
    /// wrote anything at all.
    /// </summary>
    [Fact]
    public async Task AConfirmedImport_OpensTheGurtokAndWritesThePerson()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberProfileWriteResult>(
                ResultType.Success,
                new MemberProfileWriteResult(Guid.NewGuid(), true, false, null)));
        _mediator
            .Setup(m => m.Send(It.IsAny<ProvisionMemberAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<MemberInvitation>(ResultType.Success, new MemberInvitation(Guid.NewGuid(), true)));

        var report = await _handler.Handle(
            new ImportRosterCommand(
                KurinKey,
                [Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Соколи", "2", "ivan@example.com")],
                FullMapping,
                CreateMissingGroups: true,
                DryRun: false),
            CancellationToken.None);

        report.Data!.CreatedCount.Should().Be(1);
        _groups.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(
            m => m.Send(It.IsAny<UpsertMemberProfileCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnEmptyRowInTheMiddleOfTheList_IsNotAnError()
    {
        var report = await RunAsync(
        [
            Row(2, "Петренко", "Іван", "01.01.2010", "скоб", "22.04.2024", "Ведмеді", "2", "ivan@example.com"),
            Row(4, "Коваль", "Марія", "02.02.2011", "учасник", "01.05.2023", "Ведмеді", "2", "maria@example.com")
        ]);

        report.Data!.CreatedCount.Should().Be(2);
        report.Data.Rows.Select(row => row.RowNumber).Should().Equal(2, 4);
    }
}
