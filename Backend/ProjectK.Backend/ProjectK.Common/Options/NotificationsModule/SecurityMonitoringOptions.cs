namespace ProjectK.Common.Options.NotificationsModule;

public sealed class SecurityMonitoringOptions
{
    public const string SectionName = "SecurityMonitoring";

    public int FailedLoginThreshold { get; set; } = 5;
    public int FailedMfaThreshold { get; set; } = 5;
    public TimeSpan FailedLoginWindow { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan FailedMfaWindow { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan IpChangeWindow { get; set; } = TimeSpan.FromHours(24);
}
