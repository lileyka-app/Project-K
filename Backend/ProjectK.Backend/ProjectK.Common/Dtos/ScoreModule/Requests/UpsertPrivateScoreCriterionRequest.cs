namespace ProjectK.Common.Dtos.ScoreModule.Requests;

public sealed class UpsertPrivateScoreCriterionRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}
