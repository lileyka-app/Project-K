using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule;

/// <summary>
/// The codes the kurin's own requests fail with — the kurin, its гуртки, tags and planning. The
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

    public static ServiceResult<T> NotFound<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, KurinNotFound, "Kurin was not found.");

    public static ServiceResult<T> GroupMissing<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, GroupNotFound, "Group was not found.");

    public static ServiceResult<T> ChangedMeanwhile<T>() =>
        ServiceResult<T>.Failure(ResultType.Conflict, TagsChangedMeanwhile, "The tags were changed by someone else at the same time.");
}
