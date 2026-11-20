namespace ProjectK.Common.Contracts.IdentityModule;

public interface IActivityLogger
{
    void LogAudit(
        string action,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? email = null,
        string? newEmail = null,
        string? reason = null);

    void TrackFailedLogin(string email);
    void TrackFailedMfa(string email);
    void ReportRateLimitRejection(string? policyName);
    void ReportGeoBlocked(string ip, string? countryCode);
    void TrackIpChange(Guid userId, string ip);
}
