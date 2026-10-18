using ProjectK.Common.Entities.KraiModule;

namespace ProjectK.Common.Interfaces.Modules.KraiModule;

public interface IKraiRepository
{
    Task<IReadOnlyList<Krai>> GetAllAsync(CancellationToken token = default);

    Task<bool> ExistsAsync(string code, CancellationToken token = default);

    Task<Krai?> GetAsync(string code, CancellationToken token = default);

    /// <summary>The край tracked, so a change to it is saved.</summary>
    Task<Krai?> GetForUpdateAsync(string code, CancellationToken token = default);

    void Add(Krai krai);

    /// <summary>The profile revision in force: the latest one saved for the край.</summary>
    Task<KraiProfileRevision?> GetLatestProfileRevisionAsync(string code, CancellationToken token = default);

    Task<KraiProfileRevision?> GetProfileRevisionAsync(string code, int revision, CancellationToken token = default);

    /// <summary>Every revision of the край's profile, newest first.</summary>
    Task<IReadOnlyList<KraiProfileRevision>> GetProfileRevisionsAsync(string code, CancellationToken token = default);

    /// <summary>Revisions are only ever added: the history is the undo.</summary>
    void AddProfileRevision(KraiProfileRevision revision);
}
