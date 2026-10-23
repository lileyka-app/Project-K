using System.Text.Json;
using FluentAssertions;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;

namespace ProjectK.BusinessLogic.Tests.AuditModule;

public class AuditChangesTests
{
    [Fact]
    public void Of_KeepsOnlyWhatChanged()
    {
        var changes = AuditChanges.Of(
            ("level", MemberAwardLevel.First, MemberAwardLevel.Second),
            ("note", "та сама", "та сама"),
            ("dateAcquired", new DateTime(2026, 5, 1), new DateTime(2026, 5, 1)));

        changes.Should().Equal(new AuditChange("level", "First", "Second"));
    }

    [Fact]
    public void Of_TreatsAnEmptyStringAsNoValue()
    {
        AuditChanges.Of(("note", "", null)).Should().BeEmpty();
    }

    /// <summary>Every field the card can hide is a field the journal never holds a value of.</summary>
    [Fact]
    public void EveryPrivateField_IsRedacted()
    {
        var privateFields = MemberDataFields.All.SelectMany(group => group.Fields).ToList();

        var changes = AuditChanges.Of(privateFields.Select(field => (field, (object?)"before", (object?)"after")).ToArray());

        changes.Should().HaveCount(privateFields.Count)
            .And.OnlyContain(change => change.Redacted && change.From == null && change.To == null);
    }

    [Fact]
    public void AddedAndRemoved_RecordOnlyFieldsWithAValue()
    {
        AuditChanges.Added(("level", MemberWarningLevel.Level1), ("note", null))
            .Should().Equal(new AuditChange("level", null, "Level1"));
        AuditChanges.Removed(("level", MemberWarningLevel.Level1), ("note", null))
            .Should().Equal(new AuditChange("level", "Level1", null));
    }

    [Fact]
    public void Format_WritesDatesWithoutATime_WhenThereIsNone()
    {
        AuditChanges.Format(new DateTime(2026, 5, 1)).Should().Be("2026-05-01");
        AuditChanges.Format(new DateOnly(2026, 5, 1)).Should().Be("2026-05-01");
        AuditChanges.Format(new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc)).Should().Be("2026-05-01T09:30:00Z");
    }

    [Fact]
    public void AChange_SerializesWithoutItsEmptyParts()
    {
        JsonSerializer.Serialize(new AuditChange("phoneNumber", null, null, Redacted: true))
            .Should().Be("""{"field":"phoneNumber","redacted":true}""");
        JsonSerializer.Serialize(new AuditChange("level", null, "First"))
            .Should().Be("""{"field":"level","to":"First"}""");
        JsonSerializer.Deserialize<AuditChange>("""{"field":"note","from":"a","to":"b"}""")
            .Should().Be(new AuditChange("note", "a", "b"));
    }
}
