using ProjectK.Common.Dtos.KurinModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>
/// One kurin the person stands in: how the agenda there sees them, the roles their offices there
/// amount to, and the гуртки they lead there.
/// </summary>
public sealed record AgendaKurinViewer(
    MembershipRecord Membership,
    AgendaViewerContext Viewer,
    IReadOnlyList<string> Roles,
    IReadOnlySet<Guid> LedGroupKeys);
