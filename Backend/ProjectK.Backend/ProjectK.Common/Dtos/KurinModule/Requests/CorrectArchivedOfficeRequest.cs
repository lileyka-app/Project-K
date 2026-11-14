using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>A former holding corrected: its role within the same провід, and its dates.</summary>
public sealed record CorrectArchivedOfficeRequest(LeadershipRole Role, DateOnly StartDate, DateOnly EndDate);
