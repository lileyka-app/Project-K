using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;

public class ProbeProjectRepository : BaseEntityRepository<ProbeProject>, IProbeProjectRepository
{
    public ProbeProjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ProbeProject>> GetForMemberProbeAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProjects
            .AsTracking()
            .Include(project => project.Images)
            .Where(project => project.MemberKey == memberKey && project.ProbeId == probeId)
            .OrderBy(project => project.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProbeProject?> GetWithImagesAsync(Guid projectKey, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProjects
            .AsTracking()
            .Include(project => project.Images)
            .FirstOrDefaultAsync(project => project.ProbeProjectKey == projectKey, cancellationToken);
    }

    public async Task<ThirdProbeFile?> GetFileAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        return await Context.ThirdProbeFiles
            .AsTracking()
            .FirstOrDefaultAsync(file => file.MemberKey == memberKey && file.ProbeId == probeId, cancellationToken);
    }

    public void AddFile(ThirdProbeFile file) => Context.ThirdProbeFiles.Add(file);

    public async Task LockAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        // UPDLOCK + HOLDLOCK takes a key-range lock on the unique (MemberKey, ProbeId) index, which
        // holds even before the row exists. Other providers (tests) have nothing to lock.
        if (Context.Database.IsSqlServer())
        {
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT COUNT(*) FROM ThirdProbeFiles WITH (UPDLOCK, HOLDLOCK) WHERE MemberKey = {memberKey} AND ProbeId = {probeId}",
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ThirdProbeFile>> GetFilesForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        return await Context.ThirdProbeFiles
            .AsNoTracking()
            .Where(file => memberKeys.Contains(file.MemberKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProbeProject>> GetProjectsForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        return await Context.ProbeProjects
            .AsNoTracking()
            .Where(project => memberKeys.Contains(project.MemberKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetStoredBytesAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
    {
        var pictures = await Context.ProbeProjectImages
            .Where(image => image.Project!.MemberKey == memberKey && image.Project.ProbeId == probeId)
            .SumAsync(image => (long?)image.Bytes, cancellationToken) ?? 0;
        var certificate = await Context.ThirdProbeFiles
            .Where(file => file.MemberKey == memberKey && file.ProbeId == probeId)
            .SumAsync(file => (long?)file.CampCertificateBytes, cancellationToken) ?? 0;
        return pictures + certificate;
    }

    public void AddImage(ProbeProjectImage image) => Context.ProbeProjectImages.Add(image);

    public void RemoveImages(IEnumerable<ProbeProjectImage> images) => Context.ProbeProjectImages.RemoveRange(images);

    public async Task<IReadOnlyList<string>> GetAllImageBlobNamesAsync(CancellationToken cancellationToken = default)
    {
        var pictures = await Context.ProbeProjectImages
            .AsNoTracking()
            .Select(image => image.BlobName)
            .ToListAsync(cancellationToken);
        var certificates = await Context.ThirdProbeFiles
            .AsNoTracking()
            .Where(file => file.CampCertificateBlobName != null)
            .Select(file => file.CampCertificateBlobName!)
            .ToListAsync(cancellationToken);

        return [.. pictures, .. certificates];
    }

    public override Task<IEnumerable<ProbeProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use GetForMemberProbeAsync instead.");
    }

    public async Task DeleteForMembersAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return;
        }

        // Through the tracker, like the other progress rows: an out-of-band delete would commit
        // ahead of the use case that asked for it. Pictures are loaded so they go in the same save.
        var rows = await Context.ProbeProjects
            .Include(project => project.Images)
            .Where(project => memberKeys.Contains(project.MemberKey))
            .ToListAsync(cancellationToken);

        Context.ProbeProjects.RemoveRange(rows);

        var files = await Context.ThirdProbeFiles
            .Where(file => memberKeys.Contains(file.MemberKey))
            .ToListAsync(cancellationToken);

        Context.ThirdProbeFiles.RemoveRange(files);
    }
}
