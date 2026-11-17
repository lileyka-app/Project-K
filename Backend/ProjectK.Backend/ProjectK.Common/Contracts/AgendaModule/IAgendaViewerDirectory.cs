using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Contracts.AgendaModule;

/// <summary>
/// How an account is seen by the agenda of every kurin it stands in now: the membership, the
/// agenda's view of the person there, the roles their offices there amount to and the гуртки they
/// lead. Kurins come in the order of their numbers. Read once per request by whoever needs the
/// person across kurins — the dashboard's events and tasks, the calendar link, the duty collector.
/// </summary>
public interface IAgendaViewerDirectory
{
    /// <summary>For the account behind the request's token; empty when nobody is signed in or no person stands behind it.</summary>
    Task<IReadOnlyList<AgendaKurinViewer>> GetForCurrentAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// For an account named by key rather than by the token — the calendar link, fetched by a calendar
    /// app that signs nobody in. Empty when no person stands behind it.
    /// </summary>
    Task<IReadOnlyList<AgendaKurinViewer>> GetForAccountAsync(Guid userKey, CancellationToken cancellationToken = default);
}
