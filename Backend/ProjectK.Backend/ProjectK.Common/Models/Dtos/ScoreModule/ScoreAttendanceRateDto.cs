namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record ScoreAttendanceRateDto
{
    public required Guid? AgendaCategoryKey { get; init; }
    public required string? CategoryName { get; init; }
    public required string? CategoryColorHex { get; init; }
    public required string? CategoryIcon { get; init; }
    public required int Points { get; init; }
}
