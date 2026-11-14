using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record ScoreRuleDto
{
    public required ScoreSource Source { get; init; }
    public required int Variant { get; init; }
    public required DateOnly FromDate { get; init; }
    public required int Points { get; init; }
}
