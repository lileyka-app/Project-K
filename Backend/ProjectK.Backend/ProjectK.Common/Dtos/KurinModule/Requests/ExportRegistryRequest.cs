namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>
/// Which registry columns to write, in the order they are shown. Sent in a body rather than a
/// query string only because the list is long; nothing here is personal.
/// </summary>
public sealed class ExportRegistryRequest
{
    public IReadOnlyList<string>? Columns { get; set; }
}
