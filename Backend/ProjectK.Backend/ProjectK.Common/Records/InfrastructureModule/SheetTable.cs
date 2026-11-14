namespace ProjectK.Common.Records.InfrastructureModule;

/// <summary>One sheet: what it is called, its header row, and the rows under it.</summary>
public sealed record SheetTable(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<SheetCell>> Rows);
