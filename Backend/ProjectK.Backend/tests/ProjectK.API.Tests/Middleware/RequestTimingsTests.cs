using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProjectK.API.Middleware;
using Xunit;

namespace ProjectK.API.Tests.Middleware;

/// <summary>What <c>/health</c> reports as the API's own time: real requests, recent ones only.</summary>
public class RequestTimingsTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Snapshot_IsEmpty_WithoutRequests()
    {
        var snapshot = new RequestTimings(_clock).Snapshot();

        snapshot.Should().Be(new RequestTimingSnapshot(15, 0, null, null));
    }

    [Fact]
    public void Snapshot_TakesTheMedianAndThe95th()
    {
        var timings = new RequestTimings(_clock);
        foreach (var ms in Enumerable.Range(1, 100))
        {
            timings.Record(ms);
        }

        timings.Snapshot().Should().Be(new RequestTimingSnapshot(15, 100, 50, 95));
    }

    [Fact]
    public void Snapshot_ForgetsRequestsOlderThanTheWindow()
    {
        var timings = new RequestTimings(_clock);
        timings.Record(900);
        _clock.Now += TimeSpan.FromMinutes(16);
        timings.Record(20);

        timings.Snapshot().Should().Be(new RequestTimingSnapshot(15, 1, 20, 20));
    }

    [Theory]
    [InlineData("GET", "/api/me/dues", true)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("GET", "/health", false)]
    [InlineData("GET", "/", false)]
    [InlineData("OPTIONS", "/api/me/dues", false)]
    [InlineData("HEAD", "/", false)]
    public void Counts_LeavesOutProbesAndPreflights(string method, string path, bool counted)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        RequestTimingMiddleware.Counts(context.Request).Should().Be(counted);
    }
}
