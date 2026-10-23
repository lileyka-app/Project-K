using Microsoft.Extensions.Configuration;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;

namespace ProjectK.BusinessLogic.Tests.AuthModule;

/// <summary>
/// The marks that tell a client its token predates a change: per account, per kurin, compared in
/// whole seconds the way <c>iat</c> is written, and forgotten once no token minted before them can
/// still be accepted.
/// </summary>
public class AccessChangeSignalTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 23, 12, 0, 0, 400, TimeSpan.Zero);

    private readonly FixedTimeProvider _clock = new(Start);
    private readonly AccessChangeSignal _signal;

    public AccessChangeSignalTests()
    {
        _signal = new AccessChangeSignal(
            _clock,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:ExpiresInMinutes"] = "10" }).Build());
    }

    [Fact]
    public void ATokenMintedBeforeTheMark_IsStale_ForThatAccountOnly()
    {
        var marked = Guid.NewGuid();
        var other = Guid.NewGuid();
        var mintedAt = Start.AddMinutes(-2);

        _signal.MarkUser(marked);

        Assert.True(_signal.IsStale(marked, null, mintedAt));
        Assert.False(_signal.IsStale(other, null, mintedAt));
    }

    [Fact]
    public void AKurinMark_ReachesEveryTokenMintedForThatKurin()
    {
        var kurin = Guid.NewGuid();
        var mintedAt = Start.AddMinutes(-2);

        _signal.MarkKurin(kurin);

        Assert.True(_signal.IsStale(Guid.NewGuid(), kurin, mintedAt));
        Assert.False(_signal.IsStale(Guid.NewGuid(), Guid.NewGuid(), mintedAt));
        Assert.False(_signal.IsStale(Guid.NewGuid(), null, mintedAt));
    }

    [Fact]
    public void ATokenRefreshedInTheSameSecond_IsNotStale()
    {
        var user = Guid.NewGuid();
        _signal.MarkUser(user);

        // iat drops the 400 ms; the mark keeps them. Read naively the fresh token would look older.
        var refreshedAt = DateTimeOffset.FromUnixTimeSeconds(Start.ToUnixTimeSeconds());

        Assert.False(_signal.IsStale(user, null, refreshedAt));
    }

    [Fact]
    public void AMark_IsForgotten_OnceNoEarlierTokenCanStillBeAccepted()
    {
        var user = Guid.NewGuid();
        var mintedAt = Start.AddSeconds(-30);
        _signal.MarkUser(user);

        _clock.Advance(TimeSpan.FromMinutes(14));
        Assert.True(_signal.IsStale(user, null, mintedAt));

        // Ten minutes of token and five of bearer clock skew.
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(_signal.IsStale(user, null, mintedAt));
    }
}
