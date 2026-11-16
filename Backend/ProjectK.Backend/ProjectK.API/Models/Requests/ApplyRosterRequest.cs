using ProjectK.BusinessLogic.Modules.RosterModule.Features.Import;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.API.Models.Requests;

/// <summary>
/// A roster the провід has finished mapping. The rows come back from the preview rather than from
/// a second upload, so what is confirmed is exactly what was shown on screen.
/// </summary>
public class ApplyRosterRequest
{
    public IReadOnlyList<SheetRow> Rows { get; set; } = [];

    public IReadOnlyList<ColumnMapping> Mapping { get; set; } = [];

    /// <summary>Open the гуртки the file names and the kurin does not have.</summary>
    public bool CreateMissingGroups { get; set; }

    /// <summary>Report what would happen without writing anything.</summary>
    public bool DryRun { get; set; }
}
