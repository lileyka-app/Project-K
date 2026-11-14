namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>A пільга of the край given to one membership, from a quarter on, with or without an end.</summary>
public sealed class GrantDuesConcessionRequest
{
    public string ConcessionKey { get; set; } = string.Empty;
    public QuarterRequest FromQuarter { get; set; } = new();
    public QuarterRequest? ToQuarter { get; set; }
    public string? Reason { get; set; }
}
