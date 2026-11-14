namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>«Справи»: what waits on the person, less what they hid, and how many they hid.</summary>
public sealed record MyDutiesDto
{
    public required IReadOnlyList<MyDutyDto> Items { get; init; }
    /// <summary>Duties that wait but the person hid, unchanged since.</summary>
    public required int HiddenCount { get; init; }
}
