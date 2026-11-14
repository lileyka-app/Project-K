namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>The гурток's part of the вкладка, from a quarter on.</summary>
public sealed class SetGroupDuesRateRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    public decimal GroupShare { get; set; }
}
