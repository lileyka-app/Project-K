namespace ProjectK.Common.Rules.IdentityModule;

/// <summary>
/// The rules every onboarding path shares. The lifetime lives here rather than in each handler
/// because the invitation email quotes it too, and the two drifted apart once already: the letter
/// promised thirty days for a token that died after seven.
/// </summary>
public static class OnboardingRules
{
    /// <summary>How long an invitation stays usable after it is issued.</summary>
    public const int InvitationLifetimeDays = 7;
}
