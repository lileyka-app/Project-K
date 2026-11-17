namespace ProjectK.BusinessLogic.Modules.MemberModule;

/// <summary>
/// The codes a request about a person fails with — their card, account, photo, verification,
/// відзначення and перестороги. The client picks its wording by them.
/// </summary>
public static class MemberErrors
{
    public const string MemberNotFound = "MemberNotFound";
    public const string MemberKeyRequired = "MemberKeyRequired";

    /// <summary>A new card is opened in a гурток; the form came without one.</summary>
    public const string GroupKeyRequired = "GroupKeyRequired";
    public const string MemberDeleteFailed = "MemberDeleteFailed";
    public const string MemberNotSaved = "MemberNotSaved";
    public const string KurinNotFound = "KurinNotFound";
    public const string GroupNotFound = "GroupNotFound";

    /// <summary>The person stands in no kurin, so nobody can act on their card from one.</summary>
    public const string MemberBelongsNowhere = "MemberBelongsNowhere";


    public const string UnknownDossierFolder = "UnknownDossierFolder";

    public const string EmailRequired = "EmailRequired";

    /// <summary>The address is taken by another account or registration.</summary>
    public const string EmailTaken = "EmailTaken";

    /// <summary>Contacts of a person with an account are changed by its owner.</summary>
    public const string ContactInfoLinked = "ContactInfoLinked";

    /// <summary>A required card field of the край is empty, or one is malformed.</summary>
    public const string CardFieldRequired = "CardFieldRequired";

    /// <summary>A senior's youth record (ступені, УПЮ відзначення) is changed by the person or the kurin-wide провід.</summary>
    public const string SeniorYouthRecord = "SeniorYouthRecord";

    public const string InvalidImage = "InvalidImage";

    public const string AccountNotFound = "AccountNotFound";
    public const string AccountAlreadyLinked = "AccountAlreadyLinked";
    public const string AccountEmailTaken = "AccountEmailTaken";
    public const string UserNotCreated = "UserNotCreated";
    public const string NoAccount = "NoAccount";
    public const string InvitationNotIssued = "InvitationNotIssued";

    /// <summary>A send that failed, so a caller can tell it from a refusal.</summary>
    public const string InvitationNotSent = "InvitationNotSent";

    public const string ProfileVerificationDisabled = "ProfileVerificationDisabled";
    public const string ProfileVerificationNotAllowed = "ProfileVerificationNotAllowed";

    public const string AwardNotFound = "AwardNotFound";
    public const string AwardNotSubmitted = "AwardNotSubmitted";
    public const string AwardLevelNotAllowed = "AwardLevelNotAllowed";
    public const string AwardRemovalNotAllowed = "AwardRemovalNotAllowed";
    public const string ConfirmedAwardEditNotAllowed = "ConfirmedAwardEditNotAllowed";
    public const string SeniorAwardOutsideSeniorKurin = "SeniorAwardOutsideSeniorKurin";
    public const string YouthAwardInSeniorKurin = "YouthAwardInSeniorKurin";
    public const string UnknownSeniorAward = "UnknownSeniorAward";

    public const string WarningNotFound = "WarningNotFound";
    public const string InvalidWarningLevel = "InvalidWarningLevel";
    public const string WarningLevelNotAllowed = "WarningLevelNotAllowed";

    /// <summary>The level does not follow the ones in force: a second needs a first, and no level twice.</summary>
    public const string WarningLevelOutOfOrder = "WarningLevelOutOfOrder";

    /// <summary>Already cancelled, or expired.</summary>
    public const string WarningNotActive = "WarningNotActive";
    public const string WarningCancelNotAllowed = "WarningCancelNotAllowed";

    /// <summary>The caller's identity is missing from the token.</summary>
    public const string Unauthorized = "Unauthorized";
}
