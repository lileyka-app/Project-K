using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Services.BlobStorageService;

/// <summary>
/// The files of the third probe: pictures of projects and the camp посвідка. A picture removed from
/// a text loses its row on save, a replaced посвідка its column, and a removed project or person
/// takes theirs along; what is left without a reference is swept.
/// </summary>
public sealed class ProbeProjectImageReferenceProvider : IPhotoReferenceProvider
{
    private readonly AppDbContext _db;

    public ProbeProjectImageReferenceProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
    {
        var names = await new ProbeProjectRepository(_db).GetAllImageBlobNamesAsync(cancellationToken);
        return names.Distinct(StringComparer.Ordinal).ToList();
    }
}
