using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ScoreModule;
using ProjectK.Common.Records.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>Opens a kurin's <see cref="ScoreBook"/>: the one place that knows what a read needs.</summary>
public sealed class ScoreBookReader
{
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IScoreFactReader _facts;
    private readonly TimeProvider _time;

    public ScoreBookReader(
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        IMemberDirectory members,
        IScoreFactReader facts,
        TimeProvider time)
    {
        _score = score;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _members = members;
        _facts = facts;
        _time = time;
    }

    public async Task<ScoreBook> OpenAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var settings = await _score.KurinScoreSettings.GetForKurinAsync(kurinKey, cancellationToken);
        var rules = await _score.ScoreRules.GetForKurinAsync(kurinKey, cancellationToken);
        var rates = await _score.ScoreAttendanceRates.GetForKurinAsync(kurinKey, cancellationToken);
        var items = await _score.ScoreItems.GetForKurinAsync(kurinKey, cancellationToken);
        var stages = await _score.ScoreStages.GetForKurinAsync(kurinKey, cancellationToken);
        var moves = await _score.ScoreGroupMoves.GetForKurinAsync(kurinKey, cancellationToken);
        var attendances = await _score.ScoreAttendances.GetForKurinAsync(kurinKey, cancellationToken);
        var entries = await _score.ScoreEntries.GetForKurinAsync(kurinKey, cancellationToken);
        var kurinFacts = await _facts.ForKurinAsync(kurinKey, cancellationToken);

        var inKurin = (await _memberships.GetInKurinAsync(kurinKey, cancellationToken)).ToDictionary(m => m.MembershipKey);
        var current = await _members.GetByKurinAsync(kurinKey, cancellationToken);

        // The kurin's people are those who stand in it now, but a youth who left still has a row in
        // their гурток's table below; read them by key, or that row is named «—».
        var currentKeys = current.Select(p => p.MemberKey).ToHashSet();
        var leftKeys = inKurin.Values.Select(m => m.MemberKey).Where(key => !currentKeys.Contains(key)).Distinct().ToList();
        var left = leftKeys.Count == 0
            ? []
            : await _members.GetByKeysAsync(leftKeys, cancellationToken) ?? [];
        var people = current.Concat(left).GroupBy(p => p.MemberKey).Select(g => g.First()).ToList();
        var groupNames = (await _unitOfWork.Groups.GetAllAsync(kurinKey, cancellationToken)).ToDictionary(g => g.GroupKey, g => g.Name);

        // The ledger prices a mark by its event's group, so every marked event's group is read at once.
        var eventKeys = attendances.Select(a => a.AgendaItemKey).Distinct().ToList();
        var eventCategories = await _unitOfWork.AgendaItems.GetCategoryKeysAsync(eventKeys, cancellationToken);

        var youths = inKurin.Values
            .Where(m => m.Kind == MembershipKind.Youth)
            // A youth who left keeps the гурток they left from: what they earned before leaving, and
            // the days they were there, still count for it. LeftOn bounds the days.
            .Select(m => new ScoreMember(
                m.MembershipKey,
                m.GroupKey,
                DateOnly.FromDateTime(m.JoinedAtUtc),
                m.LeftAtUtc is { } left ? DateOnly.FromDateTime(left) : null))
            .ToList();

        var algorithm = settings?.Algorithm ?? ScoreAlgorithm.Average;
        var ledger = new ScoreLedger(algorithm, today, youths, moves, rules, rates, eventCategories, attendances, entries, kurinFacts);

        var firstDay = attendances.Select(a => DateOnly.FromDateTime(a.OccurrenceStartUtc))
            .Concat(entries.Select(e => e.OccurredOn))
            .Concat(kurinFacts.Select(f => f.On))
            .DefaultIfEmpty(today)
            .Min();

        return new ScoreBook(
            kurinKey, today, algorithm, ledger, inKurin,
            people.ToDictionary(p => p.MemberKey),
            people.Where(p => p.UserKey.HasValue).GroupBy(p => p.UserKey!.Value).ToDictionary(g => g.Key, g => g.First().FullName),
            groupNames, rules, rates, items, stages, entries, attendances, firstDay);
    }

    public async Task<ScoreAttendanceBook> OpenAttendanceAsync(Guid kurinKey, CancellationToken cancellationToken)
    {
        var rates = await _score.ScoreAttendanceRates.GetForKurinAsync(kurinKey, cancellationToken);
        var attendances = await _score.ScoreAttendances.GetForKurinAsync(kurinKey, cancellationToken);
        var notHeld = await _score.ScoreNotHeldOccurrences.GetForKurinAsync(kurinKey, cancellationToken);
        return new ScoreAttendanceBook(rates, attendances, notHeld);
    }

    /// <summary>Whether one occurrence was said not to have taken place, and by whom; null when it was not.</summary>
    public Task<ScoreNotHeldOccurrence?> NotHeldAsync(Guid agendaItemKey, DateTime occurrenceStartUtc, CancellationToken cancellationToken) =>
        _score.ScoreNotHeldOccurrences.FindAsync(agendaItemKey, occurrenceStartUtc, cancellationToken);
}
