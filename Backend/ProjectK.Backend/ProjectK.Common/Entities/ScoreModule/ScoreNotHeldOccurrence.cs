namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// That one occurrence of an event did not take place — сходини cancelled that week. Keyed like
/// <see cref="ScoreAttendance"/>, by the event and the occurrence's start, and one per occurrence:
/// the database holds it unique. It takes the occurrence off the провід's «Справи»; it is the
/// opposite of a mark, so the first mark on the occurrence removes it.
/// <para>Taking it back deletes the row; the kurin's journal keeps who said it and who took it back.</para>
/// </summary>
public class ScoreNotHeldOccurrence : Entity
{
    public Guid ScoreNotHeldOccurrenceKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid AgendaItemKey { get; set; }

    /// <summary>The start of the occurrence, as a mark at it would carry it.</summary>
    public DateTime OccurrenceStartUtc { get; set; }

    public Guid? MarkedByUserKey { get; set; }
    public DateTime MarkedAtUtc { get; set; }
}
