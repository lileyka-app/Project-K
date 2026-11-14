using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// How a kurin scores, where that is one choice rather than a list. A kurin with no row scores the
/// default way: <see cref="ScoreAlgorithm.Average"/>.
/// </summary>
public class KurinScoreSettings : Entity
{
    public Guid KurinScoreSettingsKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public ScoreAlgorithm Algorithm { get; set; } = ScoreAlgorithm.Average;

    public Guid? SetByUserKey { get; set; }
}
