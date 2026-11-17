namespace ProjectK.Common.Options.IdentityModule;

/// <summary>
/// How many wrong passwords one email address may collect before sign-in to it pauses, and for how long
/// the count is kept. Bound from <c>LoginThrottle</c>.
/// </summary>
public sealed class LoginThrottleOptions
{
    public const string SectionName = "LoginThrottle";

    public int FailureLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);
}
