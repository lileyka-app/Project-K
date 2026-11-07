using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Tags;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Puts tags on people and takes them off, for both ways of doing it — one person's tags, or one
/// tag's people. Each change is one journal entry about the person, recorded before the handler saves.
/// </summary>
internal sealed class KurinTagAssignments
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinAudit _audit;

    public KurinTagAssignments(IUnitOfWork uow, IKurinAudit audit)
    {
        _uow = uow;
        _audit = audit;
    }

    /// <summary>
    /// Opens the transaction a change to these people's tags runs in and holds their tags in it, so the
    /// limit is counted against what stands and two changes cannot each add the tenth tag.
    /// </summary>
    public async Task<IUnitOfWorkTransaction> HoldAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken)
    {
        var transaction = await _uow.BeginTransactionAsync(cancellationToken);
        await _uow.KurinTags.HoldMembersAsync(memberKeys, cancellationToken);
        return transaction;
    }

    /// <summary>The people of <paramref name="memberKeys"/> that are not in the kurin now.</summary>
    public async Task<IReadOnlyCollection<Guid>> OutsideKurinAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken)
    {
        if (memberKeys.Count == 0)
        {
            return [];
        }

        var current = (await _uow.Memberships.GetRecordsInKurinAsync(kurinKey, cancellationToken))
            .Where(record => record.LeftAtUtc is null)
            .Select(record => record.MemberKey)
            .ToHashSet();
        return memberKeys.Where(key => !current.Contains(key)).ToList();
    }

    /// <summary>
    /// The people of <paramref name="gaining"/> who would carry more than the limit once each gains
    /// one more tag.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>> OverLimitAsync(
        Guid kurinKey,
        IReadOnlyCollection<Guid> gaining,
        CancellationToken cancellationToken)
    {
        if (gaining.Count == 0)
        {
            return [];
        }

        var counts = await _uow.KurinTags.CountPerMemberAsync(kurinKey, gaining, cancellationToken);
        return gaining.Where(key => counts.GetValueOrDefault(key) + 1 > KurinTagRules.MaxTagsPerMember).ToList();
    }

    public async Task PutAsync(KurinTag tag, Guid memberKey, CancellationToken cancellationToken)
    {
        _uow.KurinTags.AddAssignment(new MemberTag { KurinTagKey = tag.KurinTagKey, MemberKey = memberKey });
        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Tag, AuditActions.Tag.Put, tag.KurinTagKey, tag.Name,
                SubjectMemberKey: memberKey, KurinKey: tag.KurinKey),
            cancellationToken);
    }

    public async Task TakeOffAsync(KurinTag tag, MemberTag assignment, CancellationToken cancellationToken)
    {
        _uow.KurinTags.RemoveAssignment(assignment);
        await _audit.RecordAsync(
            new AuditRecord(AuditArea.Tag, AuditActions.Tag.TakenOff, tag.KurinTagKey, tag.Name,
                SubjectMemberKey: assignment.MemberKey, KurinKey: tag.KurinKey),
            cancellationToken);
    }
}
