namespace ProjectK.BusinessLogic.Modules.KurinModule;

/// <summary>The codes a request about a провід and its roles fails with.</summary>
public static class LeadershipErrors
{
    public const string LeadershipNotFound = "LeadershipNotFound";

    /// <summary>The request seats or unseats a role the caller may not assign.</summary>
    public const string OfficeNotAssignable = "OfficeNotAssignable";

    public const string LeadershipNotSaved = "LeadershipNotSaved";
}
