using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule;

/// <summary>
/// The codes the kurin's own requests fail with — the kurin, its гуртки, tags and planning; who belongs
/// to it, its offices and виховники, the requests between kurins, the roster and the archive. The
/// client picks its wording by them, so a code is a contract: rename one only together with the
/// clients.
/// </summary>
public static class KurinErrors
{
    public const string KurinNotFound = "KurinNotFound";
    public const string KurinKeyRequired = "KurinKeyRequired";
    public const string KurinSaveFailed = "KurinSaveFailed";
    public const string KurinDeleteFailed = "KurinDeleteFailed";
    public const string UnknownKrai = "UnknownKrai";
    public const string KurinNumberOutOfRange = "KurinNumberOutOfRange";
    public const string KurinNumberTaken = "KurinNumberTaken";

    /// <summary>A senior kurin's numbers are the administrators' to hand out; it asks through a request.</summary>
    public const string SeniorNumberByRequest = "SeniorNumberByRequest";

    public const string StanytsiaTooLong = "StanytsiaTooLong";
    public const string RegionOrCountryTooLong = "RegionOrCountryTooLong";
    public const string NamedAfterTooLong = "NamedAfterTooLong";
    public const string DescriptionTooLong = "DescriptionTooLong";

    /// <summary>A УПЮ kurin keeps its number plate: only a senior kurin uploads an emblem.</summary>
    public const string EmblemForSeniorKurins = "EmblemForSeniorKurins";

    public const string InvalidImageContent = "InvalidImageContent";

    /// <summary>Card sharing is set from inside the kurin it is for.</summary>
    public const string CardSharingOtherKurin = "CardSharingOtherKurin";

    public const string GroupNotFound = "GroupNotFound";
    public const string GroupKeyEmpty = "GroupKeyEmpty";
    public const string GroupSaveFailed = "GroupSaveFailed";
    public const string GroupDeleteFailed = "GroupDeleteFailed";

    /// <summary>A senior kurin has no гуртки at all: its people are told apart by tags.</summary>
    public const string NoGroupsInSeniorKurin = "NoGroupsInSeniorKurin";

    /// <summary>A гурток is dissolved only once its dues box is settled: money left in it would be stuck.</summary>
    public const string DissolveWithDues = "GroupBoxNotSettled";

    public const string ProbeEditionNotOffered = "ProbeEditionNotOffered";

    public const string TagNotFound = "TagNotFound";

    /// <summary>The kurin has tags off: nothing about them may be changed until they are on.</summary>
    public const string TagsDisabled = "TagsDisabled";

    public const string TagNameTaken = "TagNameTaken";
    public const string TagColorNotInPalette = "TagColorNotInPalette";

    /// <summary>The person would carry more than <c>KurinTagRules.MaxTagsPerMember</c>.</summary>
    public const string TooManyTags = "TooManyTags";

    /// <summary>A person named is not in the kurin now.</summary>
    public const string NotInKurin = "NotInKurin";

    /// <summary>Someone else changed the same tags at the same moment; nothing of this change was saved.</summary>
    public const string TagsChangedMeanwhile = "TagsChangedMeanwhile";

    public const string PlanningSessionNotFound = "PlanningSessionNotFound";

    public const string KurinBranchRequestNotFound = "KurinBranchRequestNotFound";
    public const string KurinBranchRequestAlreadySettled = "KurinBranchRequestAlreadySettled";
    public const string NotSeniorKurin = "NotSeniorKurin";
    public const string NoteTooLong = "NoteTooLong";
    public const string NothingToChange = "NothingToChange";

    /// <summary>A task archive period is outside 1..3650 days; the code keeps the agenda's name, which the client knows.</summary>
    public const string AgendaArchivePolicyRange = "AgendaArchivePolicyRange";

    /// <summary>The caller's identity is missing from the token.</summary>
    public const string Unauthorized = "Unauthorized";

    public const string LeadershipNotFound = "LeadershipNotFound";

    /// <summary>The request seats or unseats a role the caller may not assign.</summary>
    public const string OfficeNotAssignable = "OfficeNotAssignable";

    public const string LeadershipNotSaved = "LeadershipNotSaved";

    public const string MemberNotFound = "MemberNotFound";
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
    public const string MembershipRequestOtherKurin = "MembershipRequestOtherKurin";

    public const string RequestNotFound = "RequestNotFound";
    public const string RequestClosed = "RequestClosed";
    public const string RequestPending = "RequestPending";
    public const string ChooseKurin = "ChooseKurin";
    public const string NotInThatKurin = "NotInThatKurin";
    public const string SeniorKurinTakesMembers = "SeniorKurinTakesMembers";
    public const string PersonStillYouth = "PersonStillYouth";
    public const string TransferAcrossLines = "TransferAcrossLines";

    /// <summary>A person stands in one senior kurin at most; another one takes them only by «перевести».</summary>
    public const string SecondSeniorKurin = "SecondSeniorKurin";
    public const string MemberNotHere = "MemberNotHere";

    /// <summary>A roster is read for a гурток or for a kurin, never both and never neither.</summary>
    public const string RosterScopeRequired = "RosterScopeRequired";

    public const string MentorNotFound = "MentorNotFound";
    public const string MentorFromAnotherKurin = "MentorFromAnotherKurin";
    public const string MentorAlreadyAssigned = "MentorAlreadyAssigned";
    public const string MentorAssignmentNotFound = "MentorAssignmentNotFound";

    /// <summary>The roster upload carried no file.</summary>
    public const string NoFile = "NoFile";

    public const string EmptyFile = "EmptyFile";
    public const string UnreadableFile = "UnreadableFile";

    public const string ArchiveOutOfReach = "ArchiveOutOfReach";
    public const string ArchiveRecordNotFound = "ArchiveRecordNotFound";
    public const string ArchiveOwnRecord = "ArchiveOwnRecord";
    public const string ArchiveRecordCurrent = "ArchiveRecordCurrent";
    public const string ArchiveInvalidRole = "ArchiveInvalidRole";
    public const string DangerZoneClosed = "DangerZoneClosed";
    public const string WrongCredentials = "WrongCredentials";
    public const string NoSecondFactor = "NoSecondFactor";
    public const string TooManyAttempts = "TooManyAttempts";
    public const string ArchiveMembershipHasDues = "ArchiveMembershipHasDues";
    public const string ArchiveOpenOffice = "ArchiveOpenOffice";
    public const string ArchiveMembershipOverlap = "ArchiveMembershipOverlap";

    public static ServiceResult<T> NotFound<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, KurinNotFound, "Kurin was not found.");

    public static ServiceResult<T> GroupMissing<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, GroupNotFound, "Group was not found.");

    public static ServiceResult<T> ChangedMeanwhile<T>() =>
        ServiceResult<T>.Failure(ResultType.Conflict, TagsChangedMeanwhile, "The tags were changed by someone else at the same time.");

    public static ServiceResult<T> NoMember<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, NotAMember, "The account has no member record.");
}
