using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProjectK.Common.Options.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

/// <summary>
/// Counts wrong passwords per email address in memory; at <see cref="LoginThrottleOptions.FailureLimit"/>
/// sign-in to that address pauses until the window since the first failure runs out.
/// <para>
/// An address nobody registered is counted exactly like a real one, so the pause can't be used to tell
/// which addresses exist. The count lives in this process: a restart forgets it.
/// </para>
/// </summary>
public sealed class LoginThrottle : ILoginThrottle
{
    private readonly IMemoryCache _cache;
    private readonly LoginThrottleOptions _options;
    private readonly object _lock = new();

    public LoginThrottle(IMemoryCache cache, IOptions<LoginThrottleOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool IsPaused(string email) =>
        _cache.TryGetValue(Key(email), out Counter? counter) && counter!.Failures >= _options.FailureLimit;

    public void RecordFailure(string email)
    {
        var key = Key(email);
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out Counter? counter))
            {
                counter!.Failures++;
                return;
            }

            // The window runs from the first failure and is not extended by later ones, so a pause
            // always ends on time, however long someone keeps trying.
            _cache.Set(key, new Counter(), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.Window
            });
        }
    }

    public void Reset(string email) => _cache.Remove(Key(email));

    private static string Key(string email) => $"login-throttle:{email.Trim().ToUpperInvariant()}";

    private sealed class Counter
    {
        public int Failures { get; set; } = 1;
    }
}
