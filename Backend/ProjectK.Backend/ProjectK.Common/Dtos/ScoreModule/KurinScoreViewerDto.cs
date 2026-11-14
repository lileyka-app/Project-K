namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record KurinScoreViewerDto
{
    /// <summary>May score somewhere in the kurin — a sheet is theirs to open.</summary>
    public required bool CanScore { get; init; }
    public required bool CanManage { get; init; }
}
