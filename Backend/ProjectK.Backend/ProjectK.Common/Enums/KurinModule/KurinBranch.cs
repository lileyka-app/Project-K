namespace ProjectK.Common.Enums.KurinModule;

/// <summary>
/// Which пластова гілка a kurin belongs to. Everything in the system predates the distinction, so
/// existing kurins are <see cref="UPYu"/> — that is what they are, not a placeholder.
/// </summary>
public enum KurinBranch
{
    /// <summary>УПЮ — юнацтво. Probes and badges only mean something here.</summary>
    UPYu = 0,

    /// <summary>УСП — старші пластуни.</summary>
    USP = 1,

    /// <summary>УПС — пластуни-сеніори.</summary>
    UPS = 2
}
