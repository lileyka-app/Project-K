using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Dtos.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>
/// The agenda's plain mappings. An item itself is answered by <c>AgendaItemResponseFactory</c>,
/// which needs the viewer and the name lookups.
/// </summary>
internal static class AgendaMapping
{
    /// <summary>An event group as the picker and management page see it.</summary>
    public static AgendaCategoryDto ToDto(this AgendaCategory category) => new()
    {
        AgendaCategoryKey = category.AgendaCategoryKey,
        KurinKey = category.KurinKey,
        Name = category.Name,
        ColorHex = category.ColorHex,
        Icon = category.Icon,
        Capacity = category.Capacity,
        WaitlistEnabled = category.WaitlistEnabled,
        DefaultDescription = category.DefaultDescription,
        RsvpRequired = category.RsvpRequired,
        DefaultDurationMinutes = category.DefaultDurationMinutes,
        ReminderLeadMinutes = category.ReminderLeadMinutes,
        IsArchived = category.IsArchived,
        IsKurinSchedule = category.IsKurinSchedule
    };
}
