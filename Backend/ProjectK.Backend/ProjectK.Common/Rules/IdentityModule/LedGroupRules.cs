using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.Common.Rules.IdentityModule;

/// <summary>
/// The гуртки of the "own groups" scope tier: the ones an account leads in a kurin.
/// </summary>
public static class LedGroupRules
{
    /// <summary>
    /// The гуртки of the offices it sits in, then those of its mentor assignments (legacy, kept until
    /// every one is an office), then the гурток it is a member of: a group leader also covers his own
    /// гурток. Each once, in that order.
    /// </summary>
    public static IReadOnlyCollection<Guid> LedGroupKeys(LedGroupSourcesRow sources)
    {
        var led = sources.OfficeGroupKeys.Concat(sources.MentorGroupKeys);
        if (sources.OwnGroupKey is { } own)
        {
            led = led.Append(own);
        }

        return led.Distinct().ToArray();
    }
}
