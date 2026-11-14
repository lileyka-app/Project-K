using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>What an automatic source is worth from a day on; zero turns it off from then.</summary>
public sealed class SetScoreRuleRequest
{
    public ScoreSource Source { get; set; }

    /// <summary>The level for a пересторога; 0 otherwise.</summary>
    public int Variant { get; set; }

    public DateOnly FromDate { get; set; }
    public int Points { get; set; }
}
