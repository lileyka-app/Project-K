using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Reports;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>The archive's error codes, which the client tells apart, and how its records are named in the journal.</summary>
public static class ArchiveErrors
{
    public const string OutOfReach = "ArchiveOutOfReach";
    public const string NotFound = "ArchiveRecordNotFound";
    public const string OwnRecord = "ArchiveOwnRecord";
    public const string StillCurrent = "ArchiveRecordCurrent";
    public const string InvalidRole = "ArchiveInvalidRole";
    public const string DangerZoneClosed = "DangerZoneClosed";
    public const string WrongCredentials = "WrongCredentials";
    public const string NoSecondFactor = "NoSecondFactor";
    public const string TooManyAttempts = "TooManyAttempts";
    public const string HasDues = "ArchiveMembershipHasDues";
    public const string OpenOffice = "ArchiveOpenOffice";
    public const string Overlap = "ArchiveMembershipOverlap";
    public const string DissolveWithDues = "GroupBoxNotSettled";

    /// <summary>A role as the kurin names it: its own name where it gave one.</summary>
    public static string RoleLabel(LeadershipRole role, IReadOnlyDictionary<LeadershipRole, string> kurinNames) =>
        kurinNames.GetValueOrDefault(role) ?? KurinReportTerminology.LeadershipRole(role);
}
