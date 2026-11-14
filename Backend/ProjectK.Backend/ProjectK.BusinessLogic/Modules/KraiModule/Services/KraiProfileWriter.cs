using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Services;

/// <summary>The one way a profile revision is written, so a save, an import and a restore check the same.</summary>
public sealed class KraiProfileWriter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IBackendCache _cache;

    public KraiProfileWriter(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cache = cache;
    }

    public static KraiProfileDocument TryParse(string? json)
    {
        try
        {
            return KraiProfileJsonRules.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return new KraiProfileDocument();
        }
    }

    public async Task<ServiceResult<KraiProfileEditorDto>> SaveAsync(
        string kraiCode, int baseRevision, KraiProfileDocument document, string? note, CancellationToken cancellationToken)
    {
        var code = Krai.NormalizeCode(kraiCode) ?? string.Empty;
        var krai = await _unitOfWork.Krais.GetAsync(code, cancellationToken);
        if (krai is null)
        {
            return KraiErrors.NotFound<KraiProfileEditorDto>();
        }

        var normalized = KraiProfileJsonRules.Normalize(document);
        var errors = KraiProfileJsonRules.Validate(normalized);
        if (errors.Count > 0)
        {
            return ServiceResult<KraiProfileEditorDto>.Failure(ResultType.BadRequest, KraiErrors.InvalidProfile, string.Join(" ", errors));
        }

        var json = KraiProfileJsonRules.Serialize(normalized);
        if (json.Length > KraiProfileJsonRules.MaxLength)
        {
            return ServiceResult<KraiProfileEditorDto>.Failure(ResultType.BadRequest, KraiErrors.ProfileTooLarge, "The profile is too large.");
        }

        // Two admins editing at once: the second save would silently undo the first one.
        var latest = await _unitOfWork.Krais.GetLatestProfileRevisionAsync(code, cancellationToken);
        var current = latest?.Revision ?? 0;
        if (baseRevision != current)
        {
            return ServiceResult<KraiProfileEditorDto>.Failure(ResultType.Conflict, KraiErrors.ProfileChanged, "The profile was saved by someone else since this edit began; reload it.");
        }

        var trimmedNote = note?.Trim();
        var revision = new KraiProfileRevision
        {
            KraiCode = code,
            Revision = current + 1,
            SchemaVersion = KraiProfileDocument.CurrentSchemaVersion,
            Json = json,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserKey = _currentUser.UserId,
            Note = string.IsNullOrEmpty(trimmedNote) ? null : trimmedNote[..Math.Min(trimmedNote.Length, 500)]
        };
        _unitOfWork.Krais.AddProfileRevision(revision);

        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return ServiceResult<KraiProfileEditorDto>.Failure(ResultType.InternalServerError, KraiErrors.NotSaved, "The profile was not saved.");
        }

        _cache.Invalidate(BackendCacheRules.KraiProfileReads);

        return new ServiceResult<KraiProfileEditorDto>(ResultType.Success, krai.ToEditorDto(revision.Revision, normalized));
    }
}
