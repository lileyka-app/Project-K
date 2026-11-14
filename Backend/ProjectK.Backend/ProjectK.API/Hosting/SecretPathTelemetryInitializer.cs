using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Hosting;

/// <summary>
/// Application Insights keeps the full URL and an operation name built from the path; for the
/// calendar link both would hold its token (see <see cref="SecretPathRules"/>).
/// </summary>
public sealed class SecretPathTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is RequestTelemetry request && request.Url is { } url && SecretPathRules.MaskUrl(url.ToString()) is { } masked && masked != url.ToString())
        {
            request.Url = new Uri(masked);
            request.Name = SecretPathRules.MaskUrl(request.Name);
        }

        if (telemetry.Context.Operation.Name is { } operation)
        {
            telemetry.Context.Operation.Name = SecretPathRules.MaskUrl(operation);
        }
    }
}
