namespace ProjectK.API.Middleware;

/// <summary>Server-side time of real requests over a recent window, as <c>/health</c> reports it.</summary>
public sealed record RequestTimingSnapshot(int WindowMinutes, int Requests, double? P50Ms, double? P95Ms);
