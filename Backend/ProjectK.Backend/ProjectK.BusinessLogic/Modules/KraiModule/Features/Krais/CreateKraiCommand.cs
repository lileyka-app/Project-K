using MediatR;
using ProjectK.Common.Models.Dtos.KraiModule;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;

/// <summary>
/// Adds a край, such as one Plast registers after this instance was set up. The code is the
/// country's ISO 3166 code where there is one: the phone field takes its country from it.
/// </summary>
public sealed record CreateKraiCommand(string Code, string Name, KraiProfileDocument? Document) : IRequest<ServiceResult<KraiDto>>;

public sealed class CreateKraiCommandHandler : IRequestHandler<CreateKraiCommand, ServiceResult<KraiDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IBackendCache _cache;
    private readonly ICatalogKraiNames _catalogNames;

    public CreateKraiCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, IBackendCache cache, ICatalogKraiNames catalogNames)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cache = cache;
        _catalogNames = catalogNames;
    }

    public async Task<ServiceResult<KraiDto>> Handle(CreateKraiCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!KraiProfileJson.IsKraiCode(code))
        {
            return ServiceResult<KraiDto>.Failure(ResultType.BadRequest, KraiErrors.InvalidKraiCode, "A krai code is 2 to 8 lowercase latin letters, like pl or sk.");
        }

        if (KraiNames.Clean(request.Name) is not { } name)
        {
            return KraiNames.Invalid();
        }

        if (await _unitOfWork.Krais.ExistsAsync(code, cancellationToken))
        {
            return ServiceResult<KraiDto>.Failure(ResultType.Conflict, KraiErrors.KraiExists, $"Krai '{code}' already exists.");
        }

        if (await KraiNames.TakenAsync(_unitOfWork, name, except: null, cancellationToken))
        {
            return KraiNames.Taken(name);
        }

        // Left to the defaults, a край offers the вмілості marked with its own code, and the
        // catalogue marks only ua and pl: a new край would see none.
        var document = KraiProfileJson.Normalize(request.Document
            ?? new KraiProfileDocument { Catalog = new KraiCatalogDocument { BadgeSources = [Krai.Ukraine] } });
        var errors = KraiProfileJson.Validate(document);
        if (errors.Count > 0)
        {
            return ServiceResult<KraiDto>.Failure(ResultType.BadRequest, KraiErrors.InvalidProfile, string.Join(" ", errors));
        }

        var json = KraiProfileJson.Serialize(document);
        if (json.Length > KraiProfileJson.MaxLength)
        {
            return ServiceResult<KraiDto>.Failure(ResultType.BadRequest, KraiErrors.ProfileTooLarge, "The profile is too large.");
        }

        var now = DateTime.UtcNow;
        var krai = new Krai { Code = code, Name = name, CreatedAtUtc = now };
        _unitOfWork.Krais.Add(krai);
        _unitOfWork.Krais.AddProfileRevision(new KraiProfileRevision
        {
            KraiCode = code,
            Revision = 1,
            SchemaVersion = KraiProfileDocument.CurrentSchemaVersion,
            Json = json,
            CreatedAtUtc = now,
            CreatedByUserKey = _currentUser.UserId,
            Note = "Початковий профіль"
        });

        if (await _unitOfWork.SaveChangesAsync(cancellationToken) <= 0)
        {
            return ServiceResult<KraiDto>.Failure(ResultType.InternalServerError, KraiErrors.NotSaved, "The krai was not saved.");
        }

        // A read of this code before it existed may have cached "no such край".
        _cache.Invalidate(BackendCachePolicies.KraiProfileReads);
        _catalogNames.Name(code, name);

        return new ServiceResult<KraiDto>(ResultType.Success, krai.ToDto(kurinCount: 0));
    }
}
