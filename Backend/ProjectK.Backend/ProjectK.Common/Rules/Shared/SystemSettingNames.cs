namespace ProjectK.Common.Rules.Shared;

public static class SystemSettingNames
{
    public const string EnforcePrivilegedMfa = "Security__EnforcePrivilegedMFA";

    /// <summary>The random id this installation pings the status service with; made on the first ping.</summary>
    public const string TelemetryInstanceId = "Telemetry__InstanceId";
}
