using Microsoft.EntityFrameworkCore;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.Blobs;

public class MemberPhotoReferenceReader : IPhotoReferenceReader
{
    private readonly AppDbContext _db;
    public MemberPhotoReferenceReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
    {
        var memberPhotoBlobNames = await _db.Members
            .AsNoTracking()
            .Where(m => m.ProfilePhotoBlobName != null && m.ProfilePhotoBlobName != "")
            .Select(m => m.ProfilePhotoBlobName!)
            .ToListAsync(cancellationToken);

        return memberPhotoBlobNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
