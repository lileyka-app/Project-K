using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using ProjectK.Common.Security;

namespace ProjectK.API.Hosting;

/// <summary>
/// Application Insights keeps the full URL and an operation name built from the path; for the
/// calendar link both would hold its token (see <see cref="SecretPaths"/>).
/// </summary>
public sealed class SecretPathTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is RequestTelemetry request && request.Url is { } url && SecretPaths.MaskUrl(url.ToString()) is { } masked && masked != url.ToString())
        {
            request.Url = new Uri(masked);
            request.Name = SecretPaths.MaskUrl(request.Name);
        }

        if (telemetry.Context.Operation.Name is { } operation)
        {
            telemetry.Context.Operation.Name = SecretPaths.MaskUrl(operation);
        }
    }
}
