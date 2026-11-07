namespace ProjectK.Common.Models.Enums;

/// <summary>
/// What an <see cref="ProjectK.Common.Entities.KurinModule.Agenda.AgendaAssignment"/> points at.
/// The assignment's TargetKey is a KurinKey, GroupKey or MemberKey depending on this value, which is
/// how one item can be aimed at the whole kurin, specific groups and individual members at once.
/// </summary>
public enum AgendaTargetType
{
    Kurin = 0,
    Group = 1,
    Member = 2,

    /// <summary>A провід/КВ — <c>TargetKey</c> is a <c>LeadershipKey</c> (Type KV/Kurin/Group).</summary>
    Leadership = 3,

    /// <summary>
    /// Everyone carrying a kurin tag — <c>TargetKey</c> is a <c>KurinTagKey</c>. Read at the time it is
    /// asked, like a гурток: whoever gets the tag later gets what was assigned to it.
    /// </summary>
    Tag = 4
}
