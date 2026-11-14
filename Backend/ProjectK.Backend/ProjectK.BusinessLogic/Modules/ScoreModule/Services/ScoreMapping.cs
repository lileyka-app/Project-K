using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule;
using ProjectK.Common.Models.Score;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>Score rows as every screen shows them: the table, a гурток's page, the sheet and the settings.</summary>
internal static class ScoreMapping
{
    /// <summary>An entry with its names read from <paramref name="book"/>; a person's points count for the гурток they stood in that day.</summary>
    public static ScoreEntryDto ToDto(this ScoreEntry entry, ScoreBook book)
    {
        var groupKey = entry.GroupKey ?? (entry.MembershipKey is { } mk ? book.Ledger.GroupOn(mk, entry.OccurredOn) : null);
        return new ScoreEntryDto
        {
            ScoreEntryKey = entry.ScoreEntryKey,
            MembershipKey = entry.MembershipKey,
            MemberKey = entry.MembershipKey is { } key ? book.MemberKeyOf(key) : null,
            MemberName = entry.MembershipKey is { } named ? book.NameOfMembership(named) : null,
            GroupKey = groupKey,
            GroupName = book.GroupName(groupKey),
            IsForGroup = entry.GroupKey.HasValue,
            ScoreItemKey = entry.ScoreItemKey,
            ItemName = entry.ScoreItemKey is { } ik ? book.Items.FirstOrDefault(i => i.ScoreItemKey == ik)?.Name : null,
            Points = entry.Points,
            Reason = entry.Reason,
            AgendaItemKey = entry.AgendaItemKey,
            OccurrenceStartUtc = entry.OccurrenceStartUtc is { } o ? DateTime.SpecifyKind(o, DateTimeKind.Utc) : null,
            OccurredOn = entry.OccurredOn,
            CreatedByName = book.NameOfAccount(entry.CreatedByUserKey),
            CreatedAtUtc = DateTime.SpecifyKind(entry.CreatedDate, DateTimeKind.Utc)
        };
    }

    public static ScoreItemDto ToDto(this ScoreItem item) => new()
    {
        ScoreItemKey = item.ScoreItemKey,
        Name = item.Name,
        Points = item.Points,
        IsArchived = item.IsArchived
    };

    /// <summary>A гурток's row in the kurin's table, at <paramref name="place"/>.</summary>
    public static ScoreGroupRowDto ToDto(this ScoreGroupTotal total, int place, string name, bool canOpen) => new()
    {
        GroupKey = total.GroupKey,
        GroupName = name,
        Place = place,
        Score = total.Score,
        OtherScore = total.OtherScore,
        YouthPoints = total.YouthPoints,
        YouthCount = total.YouthCount,
        Average = total.Average,
        GroupPoints = total.GroupPoints,
        CanOpen = canOpen
    };
}
