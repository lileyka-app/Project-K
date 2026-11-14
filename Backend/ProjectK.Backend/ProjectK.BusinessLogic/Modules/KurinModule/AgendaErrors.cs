using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule;

/// <summary>
/// The codes the agenda's requests fail with — items, their targets and parts, event groups, RSVPs
/// and the task archive. The clients pick their wording by them, so a code is a contract.
/// </summary>
public static class AgendaErrors
{
    public const string AgendaNotFound = "AgendaNotFound";
    public const string AgendaOtherKurin = "AgendaOtherKurin";
    public const string KurinNotFound = "KurinNotFound";

    /// <summary>The current user could not be resolved from the request.</summary>
    public const string AgendaNoActor = "AgendaNoActor";

    /// <summary>The kurin's policy lets this user assign nothing at all.</summary>
    public const string AgendaAuthorForbidden = "AgendaAuthorForbidden";

    /// <summary>A target the user may not aim at.</summary>
    public const string AgendaTargetForbidden = "AgendaTargetForbidden";

    public const string AgendaEditForbidden = "AgendaEditForbidden";
    public const string AgendaDeleteForbidden = "AgendaDeleteForbidden";

    /// <summary>Nothing on the task, or on this part of it, is the user's to move.</summary>
    public const string AgendaStatusForbidden = "AgendaStatusForbidden";

    /// <summary>The way a target is done cannot change once someone has moved it.</summary>
    public const string AgendaModeLocked = "AgendaModeLocked";

    public const string AgendaNotInTarget = "AgendaNotInTarget";
    public const string AgendaPartMismatch = "AgendaPartMismatch";

    public const string AgendaNotEvent = "AgendaNotEvent";
    public const string AgendaNotOccurrence = "AgendaNotOccurrence";
    public const string AgendaNotVisible = "AgendaNotVisible";

    public const string AgendaArchiveTasksOnly = "AgendaArchiveTasksOnly";
    public const string AgendaArchivePolicyRange = "AgendaArchivePolicyRange";

    public const string CategoryNotFound = "CategoryNotFound";
    public const string CategoryOtherKurin = "CategoryOtherKurin";

    public static ServiceResult<T> NotFound<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, AgendaNotFound, "Agenda item was not found.");

    public static ServiceResult<T> OtherKurin<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, AgendaOtherKurin, "Agenda item belongs to a different kurin.");
}
