using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.ArchiveModule.Requests;

/// <summary>A former holding corrected: its role within the same провід, and its dates.</summary>
public sealed record CorrectArchivedOfficeRequest(LeadershipRole Role, DateOnly StartDate, DateOnly EndDate);
