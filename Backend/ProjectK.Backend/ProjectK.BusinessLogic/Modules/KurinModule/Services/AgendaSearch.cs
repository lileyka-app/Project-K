using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Models.Dtos.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>Search over what a card shows: title, description and the names of its targets.</summary>
public static class AgendaSearch
{
    public static bool Matches(AgendaItemDto item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();
        return item.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
            || (item.Description?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || item.Assignments.Any(a => a.Label?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }
}
