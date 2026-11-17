namespace ProjectK.BusinessLogic.Modules.AuditModule;

/// <summary>The codes a journal request fails with; the client picks its wording by them.</summary>
public static class AuditErrors
{
    /// <summary>The journal asked for is not the kurin the session acts in.</summary>
    public const string JournalOutsideKurin = "JournalOutsideKurin";

    /// <summary>The kurin's policy gives the caller no reach into the journal.</summary>
    public const string JournalReadNotAllowed = "JournalReadNotAllowed";

    public const string Unauthorized = "Unauthorized";
}
