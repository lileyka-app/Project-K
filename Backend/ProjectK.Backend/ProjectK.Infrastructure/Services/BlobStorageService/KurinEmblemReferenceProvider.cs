using Microsoft.EntityFrameworkCore;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.BlobStorageService;

/// <summary>The emblems senior kurins point at, so the orphan cleanup leaves them and sweeps the replaced ones.</summary>
public sealed class KurinEmblemReferenceProvider : IPhotoReferenceProvider
{
    private readonly AppDbContext _db;

    public KurinEmblemReferenceProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
        => await _db.Kurins
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(k => k.EmblemBlobName != null && k.EmblemBlobName != "")
            .Select(k => k.EmblemBlobName!)
            .Distinct()
            .ToListAsync(cancellationToken);
}
