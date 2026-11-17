using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// What being at an event is worth: for every event of a group (сходини, табір, захід) when
/// <see cref="AgendaCategoryKey"/> is set, or for one event or series when <see cref="AgendaItemKey"/>
/// is — and that one wins. Exactly one of the two is set. An event with neither is worth nothing.
/// <para>
/// Points are worked out from a mark of attendance when read, not kept on it: changing the rate after
/// the marks are in changes what they earned, on purpose.
/// </para>
/// </summary>
public class ScoreAttendanceRate : Entity
{
    public Guid ScoreAttendanceRateKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public Guid? AgendaCategoryKey { get; set; }
    public Guid? AgendaItemKey { get; set; }

    public int Points { get; set; }

    public Guid? SetByUserKey { get; set; }
}
