namespace ProjectK.Common.Records.InfrastructureModule;

/// <summary>One row as it was read, with the number the person sees in Excel.</summary>
public sealed record SheetRow(int Number, IReadOnlyList<string> Cells);
