using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Records.IdentityModule;

/// <summary>The person in one kurin where they hold an office, as every duty source sees them.</summary>
/// <param name="KurinKey">The kurin where they hold the office.</param>
/// <param name="Agenda">How the agenda of that kurin sees them, for sources that read events.</param>
/// <param name="Rights">What their offices there let them act on.</param>
public sealed record DutyViewer(Guid KurinKey, AgendaViewerContext Agenda, DutyRights Rights);
