using ProjectK.Common.Models.Reports;

namespace ProjectK.Common.Interfaces.Modules.KurinModule;

/// <summary>Draws a person's life story as a PDF; the library lives in the infrastructure, as for the kurin's report.</summary>
public interface ILifeStoryPdfRenderer
{
    /// <summary>The PDF bytes; the time it was made is printed in <paramref name="timeZoneId"/>, or UTC.</summary>
    byte[] Render(LifeStoryData story, string? timeZoneId = null);
}
