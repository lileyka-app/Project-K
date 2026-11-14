using ProjectK.Common.Models.Dtos.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

public record PlanningSessionResponse
{
    public Guid PlanningSessionKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KurinKey { get; set; } = string.Empty;

    // Параметри
    public DateTime SearchStart { get; set; }
    public DateTime SearchEnd { get; set; }
    public int DurationDays { get; set; }

    // Результат (може бути null, якщо ще не пораховано)
    public bool IsCalculated { get; set; }
    public DateTime? OptimalStartDate { get; set; }
    public DateTime? OptimalEndDate { get; set; }
    public double ConflictScore { get; set; }

    /// <summary>Whether the caller may withdraw this session — its author, or whole-kurin management.</summary>
    public bool CanDelete { get; set; }

    // Вкладені дані
    public List<PlanningParticipantDto> Participants { get; set; } = [];
}

public record PlanningParticipantDto
{
    public string MemberKey { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public double RoleWeight { get; set; }
    public List<DateRangeDto> BusyRanges { get; set; } = [];
}
