using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuditModule.Services;

/// <summary>
/// Stamps a journal entry — the kurin the request acts in, actor, time, the subject's name and
/// гурток as they are now — and puts it on the request's pending changes. An unscoped admin's
/// action is filed under the kurin of the person it is about. The actor comes from the request's
/// <see cref="AuditActorResolver"/>, so it is the same one the handler signed with.
/// <para>
/// Private data is stripped here as well as in <c>AuditChanges</c>: a change built by hand still
/// cannot carry a phone number into the journal.
/// </para>
/// </summary>
public sealed class KurinAudit : IKurinAudit
{
    private readonly AuditActorResolver _actors;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;
    private readonly IKurinAuditWriter _writer;
    private readonly Dictionary<(Guid? KurinKey, Guid MemberKey), Subject> _subjects = new();

    public KurinAudit(
        AuditActorResolver actors,
        ICurrentUserContext currentUser,
        IMemberDirectory members,
        IKurinAuditWriter writer)
    {
        _actors = actors;
        _currentUser = currentUser;
        _members = members;
        _writer = writer;
    }

    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var kurinKey = record.KurinKey ?? _currentUser.KurinKey;

        Subject? subject = null;
        if (record.SubjectMemberKey is { } memberKey)
        {
            subject = await DescribeAsync(kurinKey, memberKey, cancellationToken);
            kurinKey ??= subject.KurinKey;
        }

        // Nothing to file it under: an unscoped request about no one in particular.
        if (kurinKey is null)
        {
            return;
        }

        var actor = await _actors.ResolveAsync(cancellationToken);

        _writer.Add(new KurinAuditEvent
        {
            KurinKey = kurinKey.Value,
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserKey = actor.UserKey,
            ActorName = Clip(actor.Name, KurinAuditEvent.NameMaxLength),
            ActorRole = Clip(actor.Role, KurinAuditEvent.RoleMaxLength) ?? string.Empty,
            Area = record.Area,
            Action = record.Action,
            SubjectMemberKey = record.SubjectMemberKey,
            SubjectMemberName = Clip(subject?.Name, KurinAuditEvent.NameMaxLength),
            SubjectGroupKey = subject?.GroupKey,
            SubjectKey = record.SubjectKey,
            SubjectLabel = Clip(record.SubjectLabel, KurinAuditEvent.LabelMaxLength) ?? string.Empty,
            Changes = (record.Changes ?? []).Select(change => change.Sanitized()).ToList(),
            Note = Clip(record.Note, KurinAuditEvent.NoteMaxLength)
        });
    }

    /// <summary>
    /// The person as they stand in the kurin now: their name and гурток. Someone no longer in it is
    /// still named, without a гурток. Kept for the request, since one action often records twice.
    /// </summary>
    private async Task<Subject> DescribeAsync(Guid? kurinKey, Guid memberKey, CancellationToken cancellationToken)
    {
        if (_subjects.TryGetValue((kurinKey, memberKey), out var known))
        {
            return known;
        }

        MemberSummary? standing = null;
        if (kurinKey is { } inKurin)
        {
            var found = await _members.GetByKurinAsync(inKurin, new MemberSelection([memberKey], [], []), cancellationToken);
            standing = found.FirstOrDefault(member => member.MemberKey == memberKey);
        }

        Subject subject;
        if (standing is not null)
        {
            subject = new Subject(standing.FullName, standing.GroupKey, standing.KurinKey);
        }
        else
        {
            var person = await _members.FindAsync(memberKey, cancellationToken);
            subject = new Subject(person?.FullName, null, person?.KurinKey);
        }

        _subjects[(kurinKey, memberKey)] = subject;
        return subject;
    }

    private static string? Clip(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private sealed record Subject(string? Name, Guid? GroupKey, Guid? KurinKey);
}
