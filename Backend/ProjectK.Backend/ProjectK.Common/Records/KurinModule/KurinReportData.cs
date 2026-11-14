namespace ProjectK.Common.Records.KurinModule;

/// <param name="Staff">
/// The кадра виховників, by the rule in <c>KurinRoster</c> — the same rule the реєстр splits on.
/// </param>
/// <param name="Youth">Everyone else in the kurin. Together with <paramref name="Staff"/> this is the whole склад.</param>
/// <param name="LevelTally">How many юнаки stand at each ступінь of this kurin's ladder.</param>
/// <param name="Members">
/// Everyone, staff included, in one list — this is what the per-person картотека pages are built
/// from, and кадра gets a картотека like anyone else.
/// </param>
/// <param name="ShowsTags">Whether the kurin uses tags, so the report has a place for them.</param>
public sealed record KurinReportData(
    KurinReportHeader Header,
    KurinReportKurin Kurin,
    IReadOnlyList<KurinReportGroup> Groups,
    IReadOnlyList<KurinReportMember> Staff,
    IReadOnlyList<KurinReportMember> Youth,
    IReadOnlyList<KurinReportLevelCount> LevelTally,
    IReadOnlyList<KurinReportMember> Members,
    bool ShowsTags);
