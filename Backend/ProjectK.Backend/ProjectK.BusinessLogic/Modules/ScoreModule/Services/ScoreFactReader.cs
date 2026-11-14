using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <inheritdoc />
/// <remarks>
/// Asks each module through its contract and turns what it says into facts about memberships: a
/// вмілість or a пересторога is a person's, so it is put on the membership the person held in this
/// kurin on that day, or their latest one here when the day falls outside every stay. Nothing is
/// kept — a пересторога revoked or a вмілість taken back is simply not here next time.
/// </remarks>
public sealed class ScoreFactReader : IScoreFactReader
{
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberProgressDirectory _progress;
    private readonly IMemberDirectory _members;
    private readonly IDuesDirectory _dues;
    private readonly TimeProvider _time;

    public ScoreFactReader(
        IMembershipDirectory memberships,
        IMemberProgressDirectory progress,
        IMemberDirectory members,
        IDuesDirectory dues,
        TimeProvider time)
    {
        _memberships = memberships;
        _progress = progress;
        _members = members;
        _dues = dues;
        _time = time;
    }

    public async Task<IReadOnlyList<ScoreFact>> ForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var stays = (await _memberships.GetInKurinAsync(kurinKey, cancellationToken))
            .Where(m => m.Kind == MembershipKind.Youth)
            .ToLookup(m => m.MemberKey);

        var facts = new List<ScoreFact>();

        var progress = await _progress.GetFactsForKurinAsync(kurinKey, cancellationToken);
        foreach (var badge in progress.Badges)
        {
            Add(facts, stays, badge.MemberKey, ScoreSource.Skill, 0, badge.ConfirmedAtUtc);
        }

        foreach (var point in progress.ProbePoints)
        {
            Add(facts, stays, point.MemberKey, ScoreSource.ProbePoint, 0, point.SignedAtUtc);
        }

        foreach (var probe in progress.Probes)
        {
            Add(facts, stays, probe.MemberKey, ScoreSource.Probe, 0, probe.ClosedAtUtc);
        }

        foreach (var warning in await _members.GetActiveWarningsInKurinAsync(kurinKey, now, cancellationToken))
        {
            Add(facts, stays, warning.MemberKey, ScoreSource.Warning, (int)warning.Level, warning.IssuedAtUtc);
        }

        foreach (var quarter in await _dues.GetPaidQuartersAsync(kurinKey, cancellationToken))
        {
            facts.Add(new ScoreFact(quarter.MembershipKey, ScoreSource.Dues, 0, quarter.LastDay));
        }

        return facts;
    }

    private static void Add(List<ScoreFact> facts, ILookup<Guid, KurinMembershipRecord> stays, Guid memberKey, ScoreSource source, int variant, DateTime onUtc)
    {
        var day = DateOnly.FromDateTime(onUtc);
        var membership = stays[memberKey]
            .FirstOrDefault(m => DateOnly.FromDateTime(m.JoinedAtUtc) <= day && (m.LeftAtUtc is null || DateOnly.FromDateTime(m.LeftAtUtc.Value) >= day))
            ?? stays[memberKey].OrderByDescending(m => m.JoinedAtUtc).FirstOrDefault();

        if (membership is not null)
        {
            facts.Add(new ScoreFact(membership.MembershipKey, source, variant, day));
        }
    }
}
