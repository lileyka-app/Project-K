using ProjectK.Common.Models;

namespace ProjectK.API.Hosting;

/// <summary>The release this build was stamped with, as CI writes it into <c>ReleaseInfo:*</c>.</summary>
internal static class ReleaseStamp
{
    public const string VersionKey = "ReleaseInfo:Version";
    public const string Unknown = "unknown";

    /// <summary>The code name this build was stamped with, or <c>null</c> when it carries none.</summary>
    public static string? CodeName(IConfiguration config) =>
        ReleaseDisplay.CodeName(config["ReleaseInfo:Codename"] ?? config["ReleaseInfo:CodeName"]);
}
