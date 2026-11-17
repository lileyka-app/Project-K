using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// A person's ступені as a profile write carries them: the rows the form sent against the rows the
/// card holds, and which of them wait for the Звʼязковий.
/// </summary>
public static class PlastLevelHistoryRules
{
    /// <summary>The newest ступінь that counts, or null when none does. One that waits is left out.</summary>
    public static PlastLevel? LatestOf(ICollection<PlastLevelHistory> history) =>
        history.Where(entry => !entry.AwaitsConfirmation)
            .OrderByDescending(entry => entry.DateAchieved)
            .FirstOrDefault()?.PlastLevel;

    /// <summary>
    /// The ступені that count for the улад and the highest ступінь: the confirmed rows, and the stored
    /// latest that a record older than the history holds alone.
    /// </summary>
    public static IReadOnlyList<PlastLevel> CountingLevels(IEnumerable<PlastLevelHistory> history, PlastLevel? stored)
    {
        var levels = history.Where(entry => !entry.AwaitsConfirmation).Select(entry => entry.PlastLevel).ToList();
        if (stored is { } latest)
        {
            levels.Add(latest);
        }

        return levels;
    }

    /// <summary>
    /// Whether a ступінь entered this way waits for the Звʼязковий: only one that would move the person out
    /// of УПЮ — a senior ступінь they entered on their own card while their улад before the write was
    /// УПЮ, adult or not. Someone already in УСП or УПС manages their further ступені themselves; the
    /// провід's entries and every юнацький ступінь count at once.
    /// </summary>
    public static bool WaitsForConfirmation(PlastLevel level, bool enteredByThemselves, bool youthBefore) =>
        enteredByThemselves && youthBefore && PlastLadderRules.BranchOf(level) != KurinBranch.UPYu;

    /// <summary>
    /// Makes <paramref name="plastLevelHistory"/> what the form sent: rows it left out are removed, rows
    /// without a key are added in <paramref name="kurinKey"/>, the rest take the sent values. A row the
    /// write adds or moves to another ступінь waits when <see cref="WaitsForConfirmation"/> says so,
    /// and counts at once otherwise; a row left as it was keeps its state. <paramref name="youthBefore"/>
    /// says whether the person's улад before the write was УПЮ; the caller works it out where it knows the kurin.
    /// </summary>
    public static void Apply(
        Guid memberKey,
        Guid? kurinKey,
        ICollection<PlastLevelHistoryDto> plastLevelHistoryDto,
        ICollection<PlastLevelHistory> plastLevelHistory,
        bool enteredByThemselves = false,
        bool youthBefore = false)
    {
        if (plastLevelHistoryDto == null || !plastLevelHistoryDto.Any())
        {
            plastLevelHistory.Clear();
            return;
        }

        var dtoDict = plastLevelHistoryDto
            .Where(dto => dto.PlastLevelHistoryKey.HasValue && dto.PlastLevelHistoryKey != Guid.Empty)
            .ToDictionary(dto => dto.PlastLevelHistoryKey!.Value);

        var entitiesToDelete = plastLevelHistory
            .Where(e => !dtoDict.ContainsKey(e.PlastLevelHistoryKey))
            .ToList();

        foreach (var entity in entitiesToDelete)
        {
            plastLevelHistory.Remove(entity);
        }

        foreach (var dto in plastLevelHistoryDto)
        {
            if (!dto.PlastLevelHistoryKey.HasValue || dto.PlastLevelHistoryKey == Guid.Empty)
            {
                plastLevelHistory.Add(new PlastLevelHistory
                {
                    MemberKey = memberKey,
                    KurinKey = kurinKey,
                    PlastLevel = dto.PlastLevel,
                    DateAchieved = dto.DateAchieved,
                    IsPlastunka = dto.IsPlastunka,
                    AwaitsConfirmation = WaitsForConfirmation(dto.PlastLevel, enteredByThemselves, youthBefore)
                });
            }
            else
            {
                var existingHistory = plastLevelHistory
                    .FirstOrDefault(e => e.PlastLevelHistoryKey == dto.PlastLevelHistoryKey);

                if (existingHistory != null)
                {
                    if (existingHistory.PlastLevel != dto.PlastLevel)
                    {
                        existingHistory.AwaitsConfirmation = WaitsForConfirmation(dto.PlastLevel, enteredByThemselves, youthBefore);
                        existingHistory.ConfirmedByUserKey = null;
                        existingHistory.ConfirmedAtUtc = null;
                    }

                    existingHistory.PlastLevel = dto.PlastLevel;
                    existingHistory.DateAchieved = dto.DateAchieved;
                    existingHistory.IsPlastunka = dto.IsPlastunka;
                }
            }
        }
    }

    /// <summary>True when the sent rows add or drop a row, or change a row's ступінь or date.</summary>
    public static bool Differs(
        ICollection<PlastLevelHistoryDto> requested,
        ICollection<PlastLevelHistory> existing)
    {
        if (requested.Count != existing.Count)
        {
            return true;
        }

        var existingByKey = existing.ToDictionary(history => history.PlastLevelHistoryKey);
        foreach (var dto in requested)
        {
            if (!dto.PlastLevelHistoryKey.HasValue || dto.PlastLevelHistoryKey == Guid.Empty)
            {
                return true;
            }

            if (!existingByKey.TryGetValue(dto.PlastLevelHistoryKey.Value, out var entity))
            {
                return true;
            }

            if (entity.PlastLevel != dto.PlastLevel || entity.DateAchieved != dto.DateAchieved)
            {
                return true;
            }
        }

        return false;
    }
}
