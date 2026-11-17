namespace ProjectK.Common.Records.ScoreModule;

/// <summary>Days to total, both ends inclusive: a пластовий рік or a stage.</summary>
public readonly record struct ScorePeriod(DateOnly From, DateOnly To)
{
    public bool Contains(DateOnly day) => day >= From && day <= To;
}
