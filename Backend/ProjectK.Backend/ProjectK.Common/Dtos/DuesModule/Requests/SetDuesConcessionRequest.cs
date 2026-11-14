namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>A пільга switched on or off for one membership, from a quarter on.</summary>
public sealed class SetDuesConcessionRequest
{
    public QuarterRequest FromQuarter { get; set; } = new();
    public bool IsConcession { get; set; }
}
