namespace ProjectK.Common.Dtos.ScoreModule.Requests;

public sealed class UpsertScoreStageRequest
{
    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
