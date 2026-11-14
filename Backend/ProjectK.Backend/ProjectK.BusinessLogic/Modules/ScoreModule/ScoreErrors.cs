using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule;

/// <summary>The codes a score request fails with; the client picks its wording by them.</summary>
public static class ScoreErrors
{
    /// <summary>The caller does not act in this kurin, or may not score or manage here.</summary>
    public const string Forbidden = "Forbidden";

    /// <summary>What the request names is not in this kurin's score; the message says what.</summary>
    public const string ScoreNotFound = "ScoreNotFound";

    /// <summary>The same position was already given for the same occurrence, to the same target.</summary>
    public const string ItemAlreadyGiven = "ItemAlreadyGiven";

    /// <summary>An entry stays with whom it was given to; an edit may not move it.</summary>
    public const string EntryTargetFixed = "EntryTargetFixed";

    /// <summary>A rule may not start on or before the day the rule before it did.</summary>
    public const string ScoreRuleOverlap = "ScoreRuleOverlap";

    /// <summary>Someone is marked present at the occurrence, so it took place.</summary>
    public const string OccurrenceHasAttendance = "OccurrenceHasAttendance";

    /// <summary>The occurrence in the route is not an ISO 8601 UTC instant.</summary>
    public const string BadOccurrence = "BadOccurrence";

    public static ServiceResult<T> Refused<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, Forbidden, "You do not have access to this resource.");

    public static ServiceResult<T> NotFound<T>(string what) =>
        ServiceResult<T>.Failure(ResultType.NotFound, ScoreNotFound, what);
}
