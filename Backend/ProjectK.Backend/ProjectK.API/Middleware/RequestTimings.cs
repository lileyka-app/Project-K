namespace ProjectK.API.Middleware;

/// <summary>Server-side time of real requests over a recent window, as <c>/health</c> reports it.</summary>
public sealed record RequestTimingSnapshot(int WindowMinutes, int Requests, double? P50Ms, double? P95Ms);

/// <summary>
/// How long the API itself spent on the requests of the last few minutes, from the first middleware
/// to the last byte. The status page used to show the probe's round trip instead, and that was the
/// network from whichever Cloudflare colo ran the cron: 150–670 ms while the server spent 0.3.
/// Bounded in time and in count, so a burst cannot grow it.
/// </summary>
public sealed class RequestTimings
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxSamples = 5000;

    private readonly TimeProvider _time;
    private readonly Queue<(DateTimeOffset At, double Ms)> _samples = new();
    private readonly Lock _gate = new();

    public RequestTimings(TimeProvider time)
    {
        _time = time;
    }

    public void Record(double milliseconds)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            _samples.Enqueue((now, milliseconds));
            Trim(now);
        }
    }

    public RequestTimingSnapshot Snapshot()
    {
        double[] durations;
        lock (_gate)
        {
            Trim(_time.GetUtcNow());
            durations = _samples.Select(s => s.Ms).ToArray();
        }

        if (durations.Length == 0)
        {
            return new RequestTimingSnapshot((int)Window.TotalMinutes, 0, null, null);
        }

        Array.Sort(durations);
        return new RequestTimingSnapshot(
            (int)Window.TotalMinutes, durations.Length, Percentile(durations, 0.5), Percentile(durations, 0.95));
    }

    private void Trim(DateTimeOffset now)
    {
        var oldest = now - Window;
        while (_samples.Count > 0 && (_samples.Count > MaxSamples || _samples.Peek().At < oldest))
        {
            _samples.Dequeue();
        }
    }

    /// <summary>Nearest-rank percentile of an ascending array, rounded to a tenth of a millisecond.</summary>
    private static double Percentile(double[] sorted, double p)
    {
        var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
        return Math.Round(sorted[Math.Clamp(rank, 0, sorted.Length - 1)], 1);
    }
}
