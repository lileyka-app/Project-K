namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// How far one caller reaches into the kurin's archive for one action: the whole of it, or the
/// records of the гуртки they lead. The person behind the account comes with it, because nobody
/// corrects or deletes their own record — the rule probes and badges already follow.
/// </summary>
public sealed record ArchiveReachResult(bool KurinWide, IReadOnlySet<Guid> LedGroups, Guid? OwnMemberKey)
{
    /// <summary>Whether a record that touches these гуртки (none for a kurin-level one) is within reach.</summary>
    public bool Reaches(IEnumerable<Guid?> groupKeys) =>
        KurinWide || groupKeys.Any(groupKey => groupKey is { } key && LedGroups.Contains(key));

    public bool IsOwn(Guid memberKey) => OwnMemberKey == memberKey;
}
