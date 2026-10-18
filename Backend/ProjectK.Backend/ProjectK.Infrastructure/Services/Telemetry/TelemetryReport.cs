namespace ProjectK.Infrastructure.Services.Telemetry;

/// <summary>
/// What one ping says, field for field the status worker's <c>POST /v1/ping</c> (status/README.md).
/// Counts and codes only.
/// </summary>
public sealed record TelemetryReport(
    string InstanceId,
    string Version,
    string Kind,
    IReadOnlyList<string> Krais,
    int Kurins,
    int Members,
    int ActiveUsers7d,
    int ActiveUsers30d,
    long Requests24h,
    long Errors24h,
    int UptimeHours);
