namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>A calendar quarter as the client names it.</summary>
public sealed class QuarterRequest
{
    public int Year { get; set; }
    public int Number { get; set; }
}
