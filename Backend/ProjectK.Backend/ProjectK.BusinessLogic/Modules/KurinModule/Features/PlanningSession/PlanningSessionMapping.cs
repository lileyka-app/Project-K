using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Models.Dtos.KurinModule;
using PlanningSessionEntity = ProjectK.Common.Entities.KurinModule.Planning.PlanningSession;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;

internal static class PlanningSessionMapping
{
    /// <summary>A new session as asked for; the dates it finds are the handler's to fill.</summary>
    public static PlanningSessionEntity ToEntity(this CreatePlanningSessionCommand request) => new()
    {
        Name = request.Name,
        KurinKey = request.KurinKey,
        SearchStart = request.SearchStart,
        SearchEnd = request.SearchEnd,
        DurationDays = request.DurationDays,
        Participants = request.Participants
            .Select(participant => new PlanningParticipant
            {
                MemberKey = participant.MemberKey,
                FullName = participant.FullName,
                RoleWeight = participant.RoleWeight,
                BusyRanges = participant.BusyRanges
                    .Select(range => new ParticipantBusyRange { Start = range.Start, End = range.End })
                    .ToList()
            })
            .ToList()
    };

    /// <param name="canDelete">Whether this caller may withdraw it; the record cannot say.</param>
    public static PlanningSessionDto ToDto(this PlanningSessionEntity session, bool canDelete) => new(
        PlanningSessionKey: session.PlanningSessionKey,
        Name: session.Name,
        KurinKey: session.KurinKey.ToString(),
        SearchStart: session.SearchStart,
        SearchEnd: session.SearchEnd,
        DurationDays: session.DurationDays,
        IsCalculated: session.IsCalculated,
        OptimalStartDate: session.OptimalStartDate,
        OptimalEndDate: session.OptimalEndDate,
        ConflictScore: session.ConflictScore,
        CanDelete: canDelete,
        Participants: session.Participants
            .Select(participant => new PlanningParticipantDto(
                participant.MemberKey.ToString(),
                participant.FullName,
                participant.RoleWeight,
                participant.BusyRanges.Select(range => new DateRangeDto(range.Start, range.End)).ToList()))
            .ToList());
}
