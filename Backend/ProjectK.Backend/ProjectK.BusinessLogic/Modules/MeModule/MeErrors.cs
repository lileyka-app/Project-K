using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule;

/// <summary>The codes a request about the person behind the token fails with; the client picks its wording by them.</summary>
public static class MeErrors
{
    public const string NotSignedIn = "NotSignedIn";

    /// <summary>The account stands behind no member record, so there is no «me» to read.</summary>
    public const string NotAMember = "NotAMember";

    public const string CalendarFeedNotFound = "CalendarFeedNotFound";

    /// <summary>The card's allowance does not cover a permanent link.</summary>
    public const string CardLinkNotAllowed = "CardLinkNotAllowed";

    public const string CardSharingNotAllowed = "CardSharingNotAllowed";

    /// <summary>A shared card that cannot be read: an unknown or stale token, or a card no longer shared.</summary>
    public const string CardNotFound = "CardNotFound";

    /// <summary>The member record has no public id to hand over for a transfer.</summary>
    public const string PublicIdNotFound = "PublicIdNotFound";

    public const string AgendaNotFound = "AgendaNotFound";
    public const string AgendaNotVisible = "AgendaNotVisible";

    /// <summary>The event has no occurrence starting at the time named.</summary>
    public const string AgendaNotOccurrence = "AgendaNotOccurrence";

    public static ServiceResult<T> SignInRequired<T>() =>
        ServiceResult<T>.Failure(ResultType.Unauthorized, NotSignedIn, "Not signed in.");

    public static ServiceResult<T> NoMember<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, NotAMember, "The account has no member record.");

    public static ServiceResult<T> NoCard<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, CardNotFound, "No such card.");
}
