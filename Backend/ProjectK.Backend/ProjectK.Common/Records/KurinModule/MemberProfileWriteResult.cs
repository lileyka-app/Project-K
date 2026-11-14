namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// What the profile write leaves behind for the steps that run after it: the member it wrote, whether
/// it was newly created, and the photo it replaced — the callers of those steps have no other way to
/// know, because each runs as its own use case. <paramref name="AccountEmailToFollow"/> says the
/// address changed on a member whose account has to move with it. <paramref name="GroupKey"/> is the
/// гурток they stand in once written, which the response is masked against.
/// </summary>
public sealed record MemberProfileWriteResult(
    Guid MemberKey,
    bool IsCreated,
    bool WasProfileVerifiedCurrent,
    string? PreviousPhotoBlobName,
    bool AccountEmailToFollow = false,
    Guid? GroupKey = null);
