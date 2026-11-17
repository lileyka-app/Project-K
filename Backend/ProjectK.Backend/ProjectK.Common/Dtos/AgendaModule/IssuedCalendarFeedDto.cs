
namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>A freshly issued link: the only time its token leaves the server.</summary>
public sealed class IssuedCalendarFeedDto
{
    public string Token { get; set; } = string.Empty;
    public CalendarFeedStatusDto Status { get; set; } = new();
}
