namespace ProjectK.Common.Interfaces.Modules.ScoreModule;

/// <summary>
/// The score module's way to its own tables, kept off <see cref="IUnitOfWork"/> like
/// <c>IDuesUnitOfWork</c>. The same instance backs both, so a score write commits with the rest of
/// the request.
/// </summary>
public interface IScoreUnitOfWork
{
    IKurinScoreSettingsRepository KurinScoreSettings { get; }
    IScoreRuleRepository ScoreRules { get; }
    IScoreAttendanceRateRepository ScoreAttendanceRates { get; }
    IScoreItemRepository ScoreItems { get; }
    IScoreStageRepository ScoreStages { get; }
    IScoreAttendanceRepository ScoreAttendances { get; }
    IScoreNotHeldOccurrenceRepository ScoreNotHeldOccurrences { get; }
    IScoreEntryRepository ScoreEntries { get; }
    IScoreGroupMoveRepository ScoreGroupMoves { get; }
    IScoreTrailEventRepository ScoreTrailEvents { get; }
    IPrivateScoreCriterionRepository PrivateScoreCriteria { get; }
    IPrivateScoreEntryRepository PrivateScoreEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken token = default);
}
