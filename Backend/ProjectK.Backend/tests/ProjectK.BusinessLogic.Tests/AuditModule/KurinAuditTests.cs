using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.AuditModule;

/// <summary>
/// The journal stamps every entry the same way and never lets a person's private data in, however
/// the change was built.
/// </summary>
public class KurinAuditTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _actorUserKey = Guid.NewGuid();

    private Mock<ICurrentUserContext> User(Guid? kurinKey, params string[] roles)
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.UserId).Returns(_actorUserKey);
        user.SetupGet(x => x.KurinKey).Returns(kurinKey);
        user.SetupGet(x => x.Roles).Returns(roles);
        user.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string role) => roles.Contains(role));
        return user;
    }

    [Fact]
    public async Task PrivateData_IsRecordedAsChanged_WithoutItsValues()
    {
        var journal = new JournalRecorder(User(_kurinKey).Object);

        await journal.Audit.RecordAsync(new AuditRecord(
            AuditArea.Member,
            "Member.Updated",
            Guid.NewGuid(),
            "Картка",
            Changes:
            [
                new AuditChange("phoneNumber", "+380501112233", "+380509998877"),
                new AuditChange("motherPhone", null, "+380671234567"),
                new AuditChange("firstName", "Олесь", "Олександр")
            ]));

        journal.Entries.Single().Changes.Should().Equal(
            new AuditChange("phoneNumber", null, null, Redacted: true),
            new AuditChange("motherPhone", null, null, Redacted: true),
            new AuditChange("firstName", "Олесь", "Олександр"));
    }

    [Fact]
    public async Task TheSubject_IsNamedAndPlacedAsTheyStandInTheKurinNow()
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var journal = new JournalRecorder(User(_kurinKey, PolicyAccess.Vykhovnyk).Object)
            .Knows(memberKey, _kurinKey, groupKey, "Олесь", "Юнак")
            .Knows(Guid.NewGuid(), _kurinKey, null, "Марта", "Впорядниця", _actorUserKey);

        await journal.Audit.RecordAsync(new AuditRecord(AuditArea.Award, AuditActions.Award.Added, Guid.NewGuid(), "1-е відзначення", memberKey));
        await journal.Audit.RecordAsync(new AuditRecord(AuditArea.Award, AuditActions.Award.Confirmed, Guid.NewGuid(), "1-е відзначення", memberKey));

        journal.Entries.Should().HaveCount(2).And.AllSatisfy(entry =>
        {
            entry.KurinKey.Should().Be(_kurinKey);
            entry.ActorUserKey.Should().Be(_actorUserKey);
            entry.ActorName.Should().Be("Марта Впорядниця");
            entry.ActorRole.Should().Be(PolicyAccess.Vykhovnyk);
            entry.SubjectMemberName.Should().Be("Олесь Юнак");
            entry.SubjectGroupKey.Should().Be(groupKey);
        });

        // Two entries, one lookup of each: the actor and the subject are worked out once a request.
        journal.People.Verify(x => x.FindByAccountAsync(_actorUserKey, It.IsAny<CancellationToken>()), Times.Once);
        journal.People.Verify(x => x.GetByKurinAsync(_kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SomeoneWhoLeftTheKurin_IsStillNamed_WithoutAGroup()
    {
        var memberKey = Guid.NewGuid();
        var journal = new JournalRecorder(User(_kurinKey).Object)
            .Knows(memberKey, Guid.NewGuid(), Guid.NewGuid(), "Олесь", "Юнак");

        await journal.Audit.RecordAsync(new AuditRecord(AuditArea.Award, AuditActions.Award.Deleted, Guid.NewGuid(), "x", memberKey));

        var entry = journal.Entries.Single();
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectMemberName.Should().Be("Олесь Юнак");
        entry.SubjectGroupKey.Should().BeNull();
    }

    [Fact]
    public async Task AnUnscopedAdmin_IsFiledUnderTheSubjectsKurin()
    {
        var memberKey = Guid.NewGuid();
        var journal = new JournalRecorder(User(null, SystemRole.Admin).Object)
            .Knows(memberKey, _kurinKey, null, "Олесь", "Юнак");

        await journal.Audit.RecordAsync(new AuditRecord(AuditArea.Warning, AuditActions.Warning.Given, Guid.NewGuid(), "x", memberKey));

        var entry = journal.Entries.Single();
        entry.KurinKey.Should().Be(_kurinKey);
        entry.ActorRole.Should().Be(SystemRole.Admin);
    }

    [Fact]
    public async Task NothingToFileItUnder_WritesNothing()
    {
        var journal = new JournalRecorder(User(null, SystemRole.Admin).Object);

        await journal.Audit.RecordAsync(new AuditRecord(AuditArea.Settings, "Settings.Changed", Guid.NewGuid(), "x"));

        journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task AKurinNamedByTheRecord_IsWhereItIsFiled_WhateverTheRequestActsIn()
    {
        var namedKurin = Guid.NewGuid();
        var unscoped = new JournalRecorder(User(null, SystemRole.Admin).Object);
        var elsewhere = new JournalRecorder(User(_kurinKey).Object);

        await unscoped.Audit.RecordAsync(new AuditRecord(AuditArea.Settings, AuditActions.Settings.KurinChanged, namedKurin, "x", KurinKey: namedKurin));
        await elsewhere.Audit.RecordAsync(new AuditRecord(AuditArea.Settings, AuditActions.Settings.KurinChanged, namedKurin, "x", KurinKey: namedKurin));

        unscoped.Entries.Single().KurinKey.Should().Be(namedKurin);
        elsewhere.Entries.Single().KurinKey.Should().Be(namedKurin);
    }

    [Fact]
    public async Task OverlongSnapshots_AreClippedToTheirColumns()
    {
        var journal = new JournalRecorder(User(_kurinKey).Object);

        await journal.Audit.RecordAsync(new AuditRecord(
            AuditArea.Probe, AuditActions.Probe.PointSigned, Guid.NewGuid(), new string('л', 400), Note: new string('н', 3000)));

        var entry = journal.Entries.Single();
        entry.SubjectLabel.Should().HaveLength(Common.Entities.AuditModule.KurinAuditEvent.LabelMaxLength);
        entry.Note.Should().HaveLength(Common.Entities.AuditModule.KurinAuditEvent.NoteMaxLength);
    }
}
