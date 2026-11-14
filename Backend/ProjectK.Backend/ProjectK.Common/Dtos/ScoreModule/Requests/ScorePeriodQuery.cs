namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>Which days a read totals: a пластовий рік by its start year, or a stage. Neither means the current year.</summary>
public sealed class ScorePeriodQuery
{
    public int? Year { get; set; }
    public Guid? StageKey { get; set; }
}
