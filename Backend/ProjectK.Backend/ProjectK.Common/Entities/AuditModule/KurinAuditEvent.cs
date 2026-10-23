using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.AuditModule;

/// <summary>
/// One entry of a kurin's journal: who did what to whom, and what changed. Append-only and held by
/// key, with no foreign keys: the actor and the subject are kept as they were named at the time, so
/// an entry outlives the person, the гурток and the record it describes. A person's private data is
/// never stored here — see <see cref="AuditChange.Redacted"/>.
/// </summary>
public class KurinAuditEvent
{
    public const int ActionMaxLength = 64;
    public const int RoleMaxLength = 128;
    public const int NameMaxLength = 256;
    public const int LabelMaxLength = 256;
    public const int NoteMaxLength = 2000;

    public Guid KurinAuditEventKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The account that acted; null for something the system did on its own.</summary>
    public Guid? ActorUserKey { get; set; }

    /// <summary>The actor's name at the time.</summary>
    public string? ActorName { get; set; }

    /// <summary>The role that authorised the action at the time.</summary>
    public string ActorRole { get; set; } = string.Empty;

    public AuditArea Area { get; set; }

    /// <summary>One of <see cref="AuditActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The person the entry is about, when it is about a person.</summary>
    public Guid? SubjectMemberKey { get; set; }

    /// <summary>That person's name at the time.</summary>
    public string? SubjectMemberName { get; set; }

    /// <summary>
    /// That person's гурток at the time. What an own-group reader of the journal is filtered by, so
    /// an entry stays with the гурток it happened in after the person moves on.
    /// </summary>
    public Guid? SubjectGroupKey { get; set; }

    /// <summary>The record acted on: the проба, the вмілість, the відзначення, the пересторога.</summary>
    public Guid SubjectKey { get; set; }

    /// <summary>What that record was called at the time.</summary>
    public string SubjectLabel { get; set; } = string.Empty;

    public List<AuditChange> Changes { get; set; } = [];
    public string? Note { get; set; }
}
