namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>A given пільга's end and reason changed; its rule and start stay.</summary>
public sealed class UpdateDuesConcessionGrantRequest
{
    public QuarterRequest? ToQuarter { get; set; }
    public string? Reason { get; set; }
}
