namespace ProjectK.Common.Records.NotificationsModule;

public sealed record ResourceAccessDecision(bool IsAllowed, string Reason)
{
    public static ResourceAccessDecision Allow(string reason = "Access granted.") =>
        new(true, reason);

    public static ResourceAccessDecision Deny(string reason) =>
        new(false, reason);
}
