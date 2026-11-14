namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// Which of a kurin's people a caller needs: these members, everyone in these гуртки, and the
/// people behind these accounts — answered in one read, instead of the whole kurin for a few names.
/// </summary>
public sealed record MemberSelection(
    IReadOnlyCollection<Guid> MemberKeys,
    IReadOnlyCollection<Guid> GroupKeys,
    IReadOnlyCollection<Guid> AccountKeys)
{
    public static readonly MemberSelection None = new([], [], []);

    public bool IsEmpty => MemberKeys.Count == 0 && GroupKeys.Count == 0 && AccountKeys.Count == 0;
}
