using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Dtos.MemberModule;

public class PlastLevelHistoryDto
{
    public Guid? MemberKey { get; set; }
    public Guid? PlastLevelHistoryKey { get; set; }
    public PlastLevel PlastLevel { get; set; }
    public DateOnly DateAchieved { get; set; }

    /// <summary>A пластунка's ступінь rather than a пластун's (see <c>PlastLevelHistory.IsPlastunka</c>).</summary>
    public bool IsPlastunka { get; set; }

    /// <summary>The ступінь in the catalogue, for its name and відзнака; null for the day of joining. Read only.</summary>
    public string? CatalogId { get; set; }
}
