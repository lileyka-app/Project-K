using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.MemberModule.Requests;

/// <summary>A line of the person's life story as they write it.</summary>
public sealed class SaveLifeEntryRequest
{
    public LifeEntryKind Kind { get; set; }
    public TrainingCourse? Course { get; set; }
    public string? Title { get; set; }
    public int? Number { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Place { get; set; }
    public string? Commandant { get; set; }
    public string? Role { get; set; }
}
