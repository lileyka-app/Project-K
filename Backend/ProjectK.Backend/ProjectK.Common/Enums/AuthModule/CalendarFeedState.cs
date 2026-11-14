namespace ProjectK.Common.Enums.AuthModule;

/// <summary>Whether the account's calendar link was never issued, works, or was revoked.</summary>
public enum CalendarFeedState
{
    None = 0,
    Active = 1,
    Revoked = 2
}
