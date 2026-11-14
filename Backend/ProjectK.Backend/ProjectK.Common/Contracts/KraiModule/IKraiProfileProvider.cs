using ProjectK.Common.Records.KraiModule;

namespace ProjectK.Common.Contracts.KraiModule;

/// <summary>
/// The one place code asks what a край allows. Nothing outside it reads the profile tables or
/// checks a край code to decide behaviour.
/// </summary>
public interface IKraiProfileProvider
{
    /// <summary>The profile of a край; null when the instance has no such край.</summary>
    Task<KraiProfile?> GetForKraiAsync(string kraiCode, CancellationToken cancellationToken = default);

    /// <summary>The profile a kurin works under; null when the kurin is not there.</summary>
    Task<KraiProfile?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
