using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

public static class LifeEntries
{
    /// <summary>How many lines one life story may hold, so a script cannot fill the table.</summary>
    public const int MaxPerPerson = 500;

    public static MemberLifeEntryDto ToDto(MemberLifeEntry entry) => new(
        entry.MemberLifeEntryKey, entry.Kind, entry.Course, entry.Title, entry.Number,
        entry.StartDate, entry.EndDate, entry.Place, entry.Commandant, entry.Role);

    /// <summary>Copies what the person wrote, trimmed, dropping what does not belong to the kind.</summary>
    public static void Apply(MemberLifeEntry entry, SaveLifeEntryRequest request)
    {
        var training = request.Kind == LifeEntryKind.Training;
        entry.Kind = request.Kind;
        entry.Course = training ? request.Course : null;
        entry.Title = training && request.Course != TrainingCourse.Other ? null : TextRules.TrimOrNull(request.Title);
        entry.Number = training ? request.Number : null;
        entry.StartDate = request.StartDate;
        entry.EndDate = request.EndDate;
        entry.Place = TextRules.TrimOrNull(request.Place);
        entry.Commandant = TextRules.TrimOrNull(request.Commandant);
        entry.Role = TextRules.TrimOrNull(request.Role);
    }
}
