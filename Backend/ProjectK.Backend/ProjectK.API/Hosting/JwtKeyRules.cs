namespace ProjectK.API.Hosting;

/// <summary>
/// What a signing key has to be before the API agrees to start. Every access, refresh and MFA
/// challenge token is signed with it; a short or template key means anyone who reads the example
/// file can mint a session.
/// </summary>
public static class JwtKeyRules
{
    public const int MinimumLength = 32;

    /// <summary>
    /// The wording the repository's templates use for a key that is meant to be replaced. Refused on
    /// every tier but the two local ones, where the committed development key is the whole point.
    /// </summary>
    private static readonly string[] TemplateMarkers =
    [
        "replace-this",
        "replace-with",
        "change-me",
        "set-a-real"
    ];

    private static readonly string[] TiersThatMayUseATemplateKey = ["Development", "E2E"];

    /// <summary>Why the key is unacceptable on this tier, or <c>null</c> when it will do.</summary>
    public static string? Refusal(string? key, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Jwt:Key is not set. Put at least 32 random characters in Jwt__Key (PROJECTK_JWT_KEY in the self-host bundle).";
        }

        if (key.Length < MinimumLength)
        {
            return $"Jwt:Key is {key.Length} characters long; it has to be at least {MinimumLength}.";
        }

        var localTier = TiersThatMayUseATemplateKey.Contains(environmentName, StringComparer.OrdinalIgnoreCase);
        if (!localTier && TemplateMarkers.Any(marker => key.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return "Jwt:Key still holds the template value from the example file. Generate a real one before starting this tier.";
        }

        return null;
    }
}
