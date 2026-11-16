using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>
/// One kurin the person stands in: how the agenda there sees them, the roles their offices there
/// amount to, and the гуртки they lead there.
/// </summary>
public sealed record MeKurinContext(
    MembershipRecord Membership,
    AgendaViewerContext Viewer,
    IReadOnlyList<string> Roles,
    IReadOnlySet<Guid> LedGroupKeys);
