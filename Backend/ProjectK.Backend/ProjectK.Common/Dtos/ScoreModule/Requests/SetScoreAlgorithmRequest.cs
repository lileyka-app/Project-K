using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ScoreModule.Requests;

public sealed class SetScoreAlgorithmRequest
{
    public ScoreAlgorithm Algorithm { get; set; }
}
