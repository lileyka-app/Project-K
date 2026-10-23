using ProjectK.Common.Security;
using Serilog.Core;
using Serilog.Events;

namespace ProjectK.Infrastructure.Logging;

/// <summary>
/// Cuts the secret tail off request paths in every log event (see <see cref="SecretPaths"/>). One
/// enricher rather than a fix at each source: ASP.NET puts <c>RequestPath</c> on the scope of every
/// line written inside the request, the request log adds it again, and the Telegram alert prints it.
/// </summary>
public sealed class SecretPathMaskingEnricher : ILogEventEnricher
{
    private static readonly string[] PathProperties = ["RequestPath", "Path"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in PathProperties)
        {
            if (logEvent.Properties.TryGetValue(name, out var value)
                && value is ScalarValue { Value: string path }
                && SecretPaths.IsSecret(path))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, SecretPaths.Mask(path)));
            }
        }
    }
}
