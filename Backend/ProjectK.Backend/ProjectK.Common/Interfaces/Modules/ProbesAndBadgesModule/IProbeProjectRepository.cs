using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

public interface IProbeProjectRepository : IBaseEntityRepository<ProbeProject>
{
    /// <summary>One person's projects for one probe, oldest first, with their pictures; tracked.</summary>
    Task<IReadOnlyList<ProbeProject>> GetForMemberProbeAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    /// <summary>One project with its pictures, tracked.</summary>
    Task<ProbeProject?> GetWithImagesAsync(Guid projectKey, CancellationToken cancellationToken = default);

    /// <summary>The person's third-probe file, tracked; null until the first write.</summary>
    Task<ThirdProbeFile?> GetFileAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    void AddFile(ThirdProbeFile file);

    /// <summary>The third-probe files of these people, untracked: one read for a whole kurin.</summary>
    Task<IReadOnlyList<ThirdProbeFile>> GetFilesForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>The projects of these people, untracked, without pictures.</summary>
    Task<IReadOnlyList<ProbeProject>> GetProjectsForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds one person's third probe until the open transaction ends, so two requests that count
    /// projects, pictures or bytes before adding one run one after the other. The lock covers the
    /// file row and, while there is none, the place it would take.
    /// </summary>
    Task LockAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    /// <summary>What one person's pictures and посвідка of a probe take in storage.</summary>
    Task<long> GetStoredBytesAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);

    void AddImage(ProbeProjectImage image);

    void RemoveImages(IEnumerable<ProbeProjectImage> images);

    /// <summary>
    /// Every blob a project picture or a camp посвідка lives in: what the orphan cleanup must leave alone.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllImageBlobNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes these people's projects, pictures and third-probe files. No foreign key ties them to the member row;
    /// the blobs go with the next orphan sweep.
    /// </summary>
    Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default);
}
