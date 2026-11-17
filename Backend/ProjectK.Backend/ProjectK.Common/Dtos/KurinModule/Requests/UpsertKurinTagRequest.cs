namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of creating or renaming a tag.</summary>
public class UpsertKurinTagRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>One of the palette's colours.</summary>
    public string ColorHex { get; set; } = string.Empty;
}
