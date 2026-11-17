namespace ProjectK.Common.Enums.IdentityModule;

/// <summary>Why every session of an account was ended.</summary>
public enum SessionEndReason
{
    /// <summary>A new password, email or second factor.</summary>
    SecurityChange = 1,

    /// <summary>The account was suspended.</summary>
    Suspended = 2
}
