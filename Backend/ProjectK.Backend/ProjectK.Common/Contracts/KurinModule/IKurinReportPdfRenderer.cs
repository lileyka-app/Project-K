using ProjectK.Common.Models.Reports;

namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// Turns the assembled report into the document a провід downloads. The renderer lives with the
/// PDF library in the infrastructure; the use case that asks for it lives in the business layer,
/// so it asks through this.
/// </summary>
public interface IKurinReportPdfRenderer
{
    /// <summary>
    /// The PDF bytes. <paramref name="timeZoneId"/> is the reader's IANA zone, so the document
    /// prints the hours their own clock showed; absent or unknown prints UTC.
    /// </summary>
    byte[] Render(KurinReportData report, string? timeZoneId = null);
}
