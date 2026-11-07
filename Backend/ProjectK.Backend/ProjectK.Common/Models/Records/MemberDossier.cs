using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>
/// The names a dossier's folders go by. They appear in routes and in the <c>include</c> parameter, so
/// they are written once here rather than spelled out at every call site.
/// </summary>
public static class MemberDossierFolders
{
    public const string Memberships = "memberships";
    public const string Offices = "offices";
    public const string Levels = "levels";
    public const string Awards = "awards";
    public const string Warnings = "warnings";
    public const string Progress = "progress";

    public static readonly IReadOnlyCollection<string> All =
        [Memberships, Offices, Levels, Awards, Warnings, Progress];

    public static bool Exists(string name) =>
        All.Any(folder => string.Equals(folder, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One folder on the shelf: what it is called and how much is in it. The index is answered without
/// opening anything, so a caller can see what a person has before deciding what to read.
/// </summary>
public sealed record MemberDossierFolder(string Name, int Count);

/// <summary>
/// One stretch of belonging, as the kurin module reports it. Carries the kurin's own details so the
/// reader does not have to go and look them up — this crosses a module boundary, and one day a
/// network with it.
/// </summary>
public sealed record MembershipRecord(
    Guid MembershipKey,
    Guid KurinKey,
    int? KurinNumber,
    KurinBranch Branch,
    string? KurinNamedAfter,
    Guid? GroupKey,
    string? GroupName,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc,
    IReadOnlyList<KurinNumbering>? KurinBranches = null)
{
    public bool IsCurrent => LeftAtUtc is null;

    /// <summary>Every гілка of the kurin with its number there; the main one alone when only that was read.</summary>
    public IReadOnlyList<KurinNumbering> Numbering => KurinBranches ?? [new KurinNumbering(Branch, KurinNumber)];
}

/// <summary>One probe, as far as a person has taken it.</summary>
public sealed record ProbeProgressRecord(
    string ProbeId,
    ProbeProgressStatus Status,
    Guid KurinKey,
    DateTime? CompletedAtUtc,
    DateTime? VerifiedAtUtc);

/// <summary>One вмілість, as far as a person has taken it.</summary>
public sealed record BadgeProgressRecord(
    string BadgeId,
    BadgeProgressStatus Status,
    Guid KurinKey,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc);

/// <summary>What the probes and badges module knows about one person.</summary>
public sealed record MemberProgress(
    IReadOnlyCollection<ProbeProgressRecord> Probes,
    IReadOnlyCollection<BadgeProgressRecord> Badges)
{
    public static MemberProgress Empty { get; } = new([], []);

    public int Count => Probes.Count + Badges.Count;
}

/// <summary>
/// One membership of a kurin as another module needs to see it: whose it is, which гурток, what kind,
/// and for how long. Answered for the whole kurin, past memberships included.
/// </summary>
public sealed record KurinMembershipRecord(
    Guid MembershipKey,
    Guid MemberKey,
    Guid? GroupKey,
    MembershipKind Kind,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc);
