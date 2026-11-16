using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Signing a probe and confirming a badge vouch for someone else's work. A leader is a member
/// too, and a group-scoped grant always reaches one's own record, so without this a впорядник
/// or a Звʼязковий would sign their own probe.
/// </summary>
public static class SignOffRules
{
    /// <summary>The reason given when someone would sign off their own work.</summary>
    public const string OwnSignOff = "Nobody signs off their own probe or badge.";

    /// <summary>The capabilities that vouch for someone else's work.</summary>
    public static readonly IReadOnlySet<KurinCapability> Capabilities = new HashSet<KurinCapability>
    {
        KurinCapability.SignProbe,
        KurinCapability.UnsignProbe,
        KurinCapability.ReviewBadge
    };

    /// <summary>True when the pair is the one a sign-off capability is checked under.</summary>
    public static bool SignsOff(ResourceType resourceType, ResourceAction action) =>
        KurinCapabilityRules.All.Any(descriptor => Capabilities.Contains(descriptor.Key)
                                                && descriptor.Resource == resourceType
                                                && descriptor.Action == action);
}
