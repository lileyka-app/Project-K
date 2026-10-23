namespace ProjectK.Common.Models.Enums;

/// <summary>
/// The part of the kurin a journal entry is about, and what the journal filters by. Stored as a
/// number: every member keeps its number for good, and a new one takes the next free one.
/// </summary>
public enum AuditArea
{
    Probe = 1,
    Badge = 2,
    Award = 3,
    Warning = 4,
    Agenda = 5,
    Member = 6,
    Office = 7,
    Membership = 8,
    Settings = 9
}
