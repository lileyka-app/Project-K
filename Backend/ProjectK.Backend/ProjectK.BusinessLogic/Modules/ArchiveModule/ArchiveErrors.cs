namespace ProjectK.BusinessLogic.Modules.ArchiveModule;

/// <summary>The archive's error codes, which the client tells apart.</summary>
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
}
