namespace ProjectK.Common.Enums.AuthModule;

/// <summary>What a caller must know about an address before it offers an account for it.</summary>
public enum AccountAvailability
{
    /// <summary>No account and no pending waitlist entry hold this address.</summary>
    Available,

    /// <summary>An account already exists for this address.</summary>
    EmailTaken,

    /// <summary>A waitlist entry is already queued for this address; approving it is the way in.</summary>
    WaitlistPending
}
