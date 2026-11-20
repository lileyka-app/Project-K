namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>
/// Everything the звіт куреня prints about one person's progress: the проби as far as they went
/// and the вмілості confirmed. Only what is printed — a draft or a rejected вмілість
/// is not in it.
/// </summary>
public sealed record MemberProgressDetail(
    IReadOnlyList<ProbeProgressDetail> Probes,
    IReadOnlyList<BadgeConfirmedDetail> ConfirmedBadges)
{
    public static MemberProgressDetail Empty { get; } = new([], []);
}
