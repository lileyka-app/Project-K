using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// A person's ступені as a profile write carries them: the rows the form sent against the rows the
/// card holds.
/// </summary>
public static class PlastLevelHistoryRules
{
    /// <summary>The ступінь reached last, or null when none is recorded.</summary>
    public static PlastLevel? LatestOf(ICollection<PlastLevelHistory> history) =>
        history.OrderByDescending(entry => entry.DateAchieved).FirstOrDefault()?.PlastLevel;

    /// <summary>
    /// Makes <paramref name="plastLevelHistory"/> what the form sent: rows it left out are removed, rows
    /// without a key are added in <paramref name="kurinKey"/>, the rest take the sent values.
    /// </summary>
    public static void Apply(
        Guid memberKey,
        Guid? kurinKey,
        ICollection<PlastLevelHistoryDto> plastLevelHistoryDto,
        ICollection<PlastLevelHistory> plastLevelHistory)
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
                    IsPlastunka = dto.IsPlastunka
                });
            }
            else
            {
                var existingHistory = plastLevelHistory
                    .FirstOrDefault(e => e.PlastLevelHistoryKey == dto.PlastLevelHistoryKey);

                if (existingHistory != null)
                {
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
