namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

/// <summary>What the probes and badges module knows about one person.</summary>
public sealed record MemberProgress(
    IReadOnlyCollection<ProbeProgressRecord> Probes,
    IReadOnlyCollection<BadgeProgressRecord> Badges)
{
    public static MemberProgress Empty { get; } = new([], []);

    public int Count => Probes.Count + Badges.Count;
}
