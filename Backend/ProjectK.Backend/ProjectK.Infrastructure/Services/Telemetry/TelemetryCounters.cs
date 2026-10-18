namespace ProjectK.Infrastructure.Services.Telemetry;

/// <summary>
/// API requests and 5xx answers since the last ping. The middleware counts, the ping takes the
/// totals and starts again from zero, so "requests24h" is "since the previous ping".
/// </summary>
public sealed class TelemetryCounters
{
    private long _requests;
    private long _errors;

    public void Count(int statusCode)
    {
        Interlocked.Increment(ref _requests);
        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _errors);
        }
    }

    public (long Requests, long Errors) TakeAndReset() =>
        (Interlocked.Exchange(ref _requests, 0), Interlocked.Exchange(ref _errors, 0));
}
