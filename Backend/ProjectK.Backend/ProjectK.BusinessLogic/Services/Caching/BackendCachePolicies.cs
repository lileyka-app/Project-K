namespace ProjectK.BusinessLogic.Services.Caching;

public static class BackendCachePolicies
{
    public static readonly TimeSpan EntityReadTtl = TimeSpan.FromMinutes(1);

    public static readonly CachePolicy KurinReads = new(
        Prefix: "kurin",
        Ttl: EntityReadTtl,
        Scope: CacheScope.Shared);

    public static readonly CachePolicy GroupReads = new(
        Prefix: "group",
        Ttl: EntityReadTtl,
        Scope: CacheScope.Shared);

    // A край's profile changes only when an admin saves it, and that save drops this prefix.
    public static readonly CachePolicy KraiProfileReads = new(
        Prefix: "krai-profile",
        Ttl: TimeSpan.FromMinutes(5),
        Scope: CacheScope.Shared);

    // Read on every permission resolution; saving the policy drops this prefix, so the TTL only
    // bounds how stale another instance may be.
    public static readonly CachePolicy KurinAccessPolicyReads = new(
        Prefix: "kurin-access-policy",
        Ttl: EntityReadTtl,
        Scope: CacheScope.Shared);

    public static readonly CachePolicy SystemSettingReads = new(
        Prefix: "system-setting",
        Ttl: TimeSpan.FromMinutes(5),
        Scope: CacheScope.Shared);

    // A mentor's group set is re-read on every write-authorization check but barely
    // ever changes. Scope is per user + permission context so one mentor's set never
    // leaks to another; assign/revoke invalidate this prefix so a change takes effect
    // at once rather than waiting out the TTL.
    public static readonly CachePolicy MentorScopeReads = new(
        Prefix: "mentor-scope",
        Ttl: EntityReadTtl,
        Scope: CacheScope.UserPermissionContext);
}
