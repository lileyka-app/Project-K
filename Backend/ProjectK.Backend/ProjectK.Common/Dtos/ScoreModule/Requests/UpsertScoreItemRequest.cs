namespace ProjectK.Common.Dtos.ScoreModule.Requests;

public sealed class UpsertScoreItemRequest
{
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsArchived { get; set; }
}
