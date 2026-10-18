using MediatR;
using ProjectK.BusinessLogic.Modules.KraiModule.Models;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Manage;

/// <summary>
/// Adds a край, such as one Plast registers after this instance was set up. The code is the
/// country's ISO 3166 code where there is one: the phone field takes its country from it.
/// </summary>
public sealed record CreateKraiCommand(string Code, string Name, KraiProfileDocument? Document) : IRequest<ServiceResult<KraiResponse>>;

/// <summary>Renames a край. Its code stays: kurins, profiles and the catalogue point at it.</summary>
public sealed record RenameKraiCommand(string Code, string Name) : IRequest<ServiceResult<KraiResponse>>;

public sealed class CreateKraiCommandHandler : IRequestHandler<CreateKraiCommand, ServiceResult<KraiResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IBackendCache _cache;

    public CreateKraiCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<ServiceResult<KraiResponse>> Handle(CreateKraiCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!KraiProfileJson.IsKraiCode(code))
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.BadRequest, "InvalidKraiCode", "Код краю — від 2 до 8 латинських літер, як pl чи sk.");
        }

        if (KraiNames.Clean(request.Name) is not { } name)
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.BadRequest, "InvalidKraiName", $"Назва краю — від 1 до {KraiNames.MaxLength} символів.");
        }

        if (await _unitOfWork.Krais.ExistsAsync(code, cancellationToken))
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.Conflict, "KraiExists", $"Край «{code}» уже є.");
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
            return ServiceResult<KraiResponse>.Failure(ResultType.BadRequest, "InvalidProfile", string.Join(" ", errors));
        }

        var json = KraiProfileJson.Serialize(document);
        if (json.Length > KraiProfileJson.MaxLength)
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.BadRequest, "InvalidProfile", "Профіль завеликий.");
        }

        var now = DateTime.UtcNow;
        _unitOfWork.Krais.Add(new Krai { Code = code, Name = name, CreatedAtUtc = now });
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
            return new ServiceResult<KraiResponse>(ResultType.InternalServerError);
        }

        // A read of this code before it existed may have cached "no such край".
        _cache.Invalidate(BackendCachePolicies.KraiProfileReads);

        return new ServiceResult<KraiResponse>(ResultType.Success, new KraiResponse { Code = code, Name = name, KurinCount = 0 });
    }
}

public sealed class RenameKraiCommandHandler : IRequestHandler<RenameKraiCommand, ServiceResult<KraiResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;

    public RenameKraiCommandHandler(IUnitOfWork unitOfWork, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ServiceResult<KraiResponse>> Handle(RenameKraiCommand request, CancellationToken cancellationToken)
    {
        if (KraiNames.Clean(request.Name) is not { } name)
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.BadRequest, "InvalidKraiName", $"Назва краю — від 1 до {KraiNames.MaxLength} символів.");
        }

        var code = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        var krai = await _unitOfWork.Krais.GetForUpdateAsync(code, cancellationToken);
        if (krai is null)
        {
            return ServiceResult<KraiResponse>.Failure(ResultType.NotFound, "KraiNotFound", "Krai not found.");
        }

        if (krai.Name != name && await KraiNames.TakenAsync(_unitOfWork, name, except: krai.Code, cancellationToken))
        {
            return KraiNames.Taken(name);
        }

        if (krai.Name != name)
        {
            krai.Name = name;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _cache.Invalidate(BackendCachePolicies.KraiProfileReads);
        }

        var kurins = await _unitOfWork.Kurins.GetAllAsync(cancellationToken);
        return new ServiceResult<KraiResponse>(ResultType.Success, new KraiResponse
        {
            Code = krai.Code,
            Name = krai.Name,
            KurinCount = kurins.Count(k => k.KraiCode == krai.Code)
        });
    }
}

internal static class KraiNames
{
    public const int MaxLength = 120;

    /// <summary>
    /// Whether another край already goes by this name. Pickers show names only, so two краї with one
    /// name could not be told apart.
    /// </summary>
    public static async Task<bool> TakenAsync(IUnitOfWork unitOfWork, string name, string? except, CancellationToken cancellationToken)
    {
        return (await unitOfWork.Krais.GetAllAsync(cancellationToken))
            .Any(k => k.Code != except && string.Equals(k.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
    }

    public static ServiceResult<KraiResponse> Taken(string name) =>
        ServiceResult<KraiResponse>.Failure(ResultType.Conflict, "KraiNameTaken", $"Край з назвою «{name}» уже є.");

    public static string? Clean(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength ? null : trimmed;
    }
}
