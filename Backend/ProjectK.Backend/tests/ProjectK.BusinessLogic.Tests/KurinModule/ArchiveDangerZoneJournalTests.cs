using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>Entering, refusing and ending the danger zone write one shape of journal entry.</summary>
public class ArchiveDangerZoneJournalTests
{
    [Fact]
    public void JournalEntry_IsAboutTheAccount_InTheKurin_WithOneDetail()
    {
        var userKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();

        var entry = ArchiveDangerZoneService.JournalEntry(AuditActionNames.Archive.DangerZoneEnded, userKey, kurinKey, "reason", "spent");

        entry.Area.Should().Be(AuditArea.Archive);
        entry.Action.Should().Be(AuditActionNames.Archive.DangerZoneEnded);
        entry.SubjectKey.Should().Be(userKey);
        entry.SubjectLabel.Should().Be("Небезпечний режим");
        entry.SubjectMemberKey.Should().BeNull();
        entry.Note.Should().BeNull();
        entry.KurinKey.Should().Be(kurinKey);
        entry.Changes.Should().BeEquivalentTo([new AuditChange("reason", null, "spent")]);
    }
}
