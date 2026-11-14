namespace ProjectK.BusinessLogic.Modules.KurinModule;

/// <summary>
/// The codes a request about where a person stands fails with: joining, leaving, moving between
/// гуртки, and the requests and invitations between kurins.
/// </summary>
public static class MembershipErrors
{
    public const string MemberNotFound = "MemberNotFound";
    public const string KurinNotFound = "KurinNotFound";
    public const string KurinKeyRequired = "KurinKeyRequired";
    public const string MembershipKeysRequired = "MembershipKeysRequired";
    public const string PublicIdRequired = "PublicIdRequired";

    /// <summary>Malformed or nobody's: the answer is the same, so codes cannot be mapped out.</summary>
    public const string NoSuchCode = "NoSuchCode";

    public const string GroupNotInKurin = "GroupNotInKurin";
    public const string AlreadyAMember = "AlreadyAMember";
    public const string StandsInAnotherKurin = "StandsInAnotherKurin";
    public const string NotAMember = "NotAMember";
    public const string OwnMembership = "OwnMembership";
    public const string MemberOwesDues = "MemberOwesDues";

    /// <summary>A kurin reads and answers only its own requests.</summary>
    public const string OtherKurin = "MembershipRequestOtherKurin";

    public const string RequestNotFound = "RequestNotFound";
    public const string RequestClosed = "RequestClosed";
    public const string RequestPending = "RequestPending";
    public const string ChooseKurin = "ChooseKurin";
    public const string NotInThatKurin = "NotInThatKurin";
    public const string SeniorKurinTakesMembers = "SeniorKurinTakesMembers";
    public const string TransferAcrossLines = "TransferAcrossLines";
    public const string MemberNotHere = "MemberNotHere";
}
