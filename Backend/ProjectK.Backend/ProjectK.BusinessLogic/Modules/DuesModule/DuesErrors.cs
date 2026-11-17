using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule;

/// <summary>The codes a dues request fails with; the client picks its wording by them.</summary>
public static class DuesErrors
{
    /// <summary>The caller does not act in this kurin, or may not do this here.</summary>
    public const string Forbidden = "Forbidden";

    public const string GroupNotFound = "GroupNotFound";
    public const string KraiNotFound = "KraiNotFound";
    public const string InvalidQuarter = "InvalidQuarter";

    /// <summary>No such operation in this box, or it was deleted.</summary>
    public const string EntryNotFound = "EntryNotFound";

    /// <summary>A verified operation is locked until whoever verified it takes the mark off.</summary>
    public const string EntryVerified = "EntryVerified";

    /// <summary>The kurin has confirmed this transfer, so the гурток may no longer change it.</summary>
    public const string TransferReceived = "TransferReceived";

    public const string TransferNotFound = "TransferNotFound";

    /// <summary>The person has no вкладка in this гурток: not a youth of it, and never charged here.</summary>
    public const string NotOfThisGroup = "NotOfThisGroup";

    /// <summary>The person has no вкладка in the kurin's own box.</summary>
    public const string NotOfThisKurin = "NotOfThisKurin";

    /// <summary>The kurin has гуртки, so people pay their гурток, not the kurin.</summary>
    public const string DuesPaidInGroup = "DuesPaidInGroup";

    /// <summary>The край does not take вкладка the way the operation hands it up.</summary>
    public const string DuesTransferOff = "DuesTransferOff";

    public const string CollectorNotInKurin = "CollectorNotInKurin";

    /// <summary>The person is not a youth of this гурток now.</summary>
    public const string NotInGroup = "NotInGroup";

    /// <summary>The край of the kurin has no such пільга.</summary>
    public const string UnknownConcession = "UnknownConcession";

    public const string EndsBeforeStart = "EndsBeforeStart";

    /// <summary>The same пільга already covers some of these quarters.</summary>
    public const string ConcessionOverlaps = "ConcessionOverlaps";

    public const string GrantNotFound = "GrantNotFound";

    /// <summary>Rates by tag are only for kurins without гуртки.</summary>
    public const string TagRatesOff = "TagRatesOff";

    public const string TagsOff = "TagsOff";
    public const string TagNotFound = "TagNotFound";

    /// <summary>A стаття sends money somewhere the край does not take it.</summary>
    public const string DuesRecipientOff = "DuesRecipientOff";

    /// <summary>Three bare amounts would wipe the kurin's own статті.</summary>
    public const string DuesArticlesInUse = "DuesArticlesInUse";

    public static ServiceResult<T> Refused<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, Forbidden, "You do not have access to this resource.");

    public static ServiceResult<T> NoGroup<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, GroupNotFound, "There is no such гурток.");

    public static ServiceResult<T> NoEntry<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, EntryNotFound, "There is no such operation here.");

    /// <summary>A verified operation, which <paramref name="unmarkedBy"/> has to unmark before it changes.</summary>
    public static ServiceResult<T> Locked<T>(string unmarkedBy) =>
        ServiceResult<T>.Failure(ResultType.Conflict, EntryVerified,
            $"A verified operation is locked. Add a correction, or have the {unmarkedBy} unmark it.");

    /// <summary>The account stands behind no member record, so there is no «me» to read.</summary>
    public const string NotAMember = "NotAMember";

    public static ServiceResult<T> NoMember<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, NotAMember, "The account has no member record.");
}
