using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule;

/// <summary>
/// The codes a край or profile request fails with. The admin page words them itself
/// (<c>KRAI_ERROR_TEXT</c>); the message is for developers.
/// </summary>
public static class KraiErrors
{
    /// <summary>A край code is 2 to 8 lowercase latin letters.</summary>
    public const string InvalidKraiCode = "InvalidKraiCode";

    public const string InvalidKraiName = "InvalidKraiName";
    public const string KraiExists = "KraiExists";

    /// <summary>Pickers show names only, so two краї may not share one.</summary>
    public const string KraiNameTaken = "KraiNameTaken";

    public const string KraiNotFound = "KraiNotFound";
    public const string KurinNotFound = "KurinNotFound";
    public const string RevisionNotFound = "RevisionNotFound";

    /// <summary>The profile breaks a rule of <c>KraiProfileJson.Validate</c>; the message lists which.</summary>
    public const string InvalidProfile = "InvalidProfile";

    /// <summary>The profile is longer than <c>KraiProfileJson.MaxLength</c> once written.</summary>
    public const string ProfileTooLarge = "ProfileTooLarge";

    /// <summary>Someone saved a newer revision since the editor loaded its base one.</summary>
    public const string ProfileChanged = "ProfileChanged";

    /// <summary>The database took none of the change.</summary>
    public const string NotSaved = "KraiNotSaved";

    public static ServiceResult<T> NotFound<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, KraiNotFound, "Krai not found.");
}
