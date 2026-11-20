using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// A named stretch of days to total separately — «осінь», «до табору». Only a slice of the year: it
/// owns no points, so stages may overlap and deleting one loses nothing.
/// </summary>
public class ScoreStage : Entity
{
    public Guid ScoreStageKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly FromDate { get; set; }

    /// <summary>The last day, inclusive.</summary>
    public DateOnly ToDate { get; set; }
}
