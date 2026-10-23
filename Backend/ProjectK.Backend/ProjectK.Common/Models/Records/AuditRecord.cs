using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// What a handler tells the kurin's journal. The kurin, the actor, the time and the subject's name
/// and гурток are filled in by the journal itself, so every entry is stamped the same way.
/// </summary>
/// <param name="Area">The part of the kurin it is about.</param>
/// <param name="Action">One of <see cref="AuditActions"/>.</param>
/// <param name="SubjectKey">The record acted on.</param>
/// <param name="SubjectLabel">What that record is called now.</param>
/// <param name="SubjectMemberKey">The person it is about, when it is about a person.</param>
/// <param name="Changes">What changed; see <see cref="AuditChanges"/>.</param>
/// <param name="Note">A note the actor left with it.</param>
public sealed record AuditRecord(
    AuditArea Area,
    string Action,
    Guid SubjectKey,
    string SubjectLabel,
    Guid? SubjectMemberKey = null,
    IReadOnlyList<AuditChange>? Changes = null,
    string? Note = null);
