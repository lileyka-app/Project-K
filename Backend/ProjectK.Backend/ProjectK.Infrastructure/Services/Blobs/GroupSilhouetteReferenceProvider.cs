using Microsoft.EntityFrameworkCore;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.Blobs;

public sealed class GroupSilhouetteReferenceProvider : IPhotoReferenceProvider
{
    private readonly AppDbContext _db;

    public GroupSilhouetteReferenceProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
    {
        var silhouetteBlobNames = await _db.Groups
            .AsNoTracking()
            .Where(g => g.SilhouetteBlobName != null && g.SilhouetteBlobName != "")
            .Select(g => g.SilhouetteBlobName!)
            .ToListAsync(cancellationToken);

        return silhouetteBlobNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
