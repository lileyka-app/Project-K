using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

namespace ProjectK.API.Models.Requests;

/// <summary>
/// The roster being uploaded. A model rather than a bare <see cref="IFormFile"/> for the same
/// reason as <see cref="UploadImageRequest"/>: Swashbuckle will not describe the bare form.
/// </summary>
public class UploadRosterRequest
{
    public IFormFile? File { get; set; }
}

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
