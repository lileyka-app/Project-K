using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.KraiModule;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public class UpsertKurinCommand : IRequest<ServiceResult<KurinDto>>
{
    public Guid KurinKey { get; set; }

    /// <summary>The kurin's number; null makes it a підготовчий курінь.</summary>
    public int? Number { get; set; }
    public string? Stanytsia { get; set; }
    public string? RegionOrCountry { get; set; }
    public string? NamedAfter { get; set; }
    public string? Description { get; set; }
    public bool ProfileVerificationEnabled { get; set; }

    /// <summary>Точкування on or off; null leaves the kurin as it is.</summary>
    public bool? ScoreEnabled { get; set; }

    /// <summary>The kurin's own names for its roles, by role name; null leaves them as they are, a blank name restores the usual one.</summary>
    public IReadOnlyDictionary<string, string?>? RoleNames { get; set; }

    /// <summary>
    /// The край a new kurin joins; null takes the instance default. An existing kurin keeps its
    /// край here and moves only through <c>SetKurinKraiCommand</c>, which only an admin may send.
    /// </summary>
    public string? KraiCode { get; set; }

    public UpsertKurinCommand(Guid kurinKey, int? number, string? stanytsia = null, string? regionOrCountry = null, string? namedAfter = null, string? description = null, bool profileVerificationEnabled = false)
    {
        KurinKey = kurinKey;
        Number = number;
        Stanytsia = stanytsia;
        RegionOrCountry = regionOrCountry;
        NamedAfter = namedAfter;
        Description = description;
        ProfileVerificationEnabled = profileVerificationEnabled;
    }
    public UpsertKurinCommand(int? number, string? kraiCode = null)
    {
        Number = number;
        KraiCode = kraiCode;
    }
}

public class UpsertKurinCommandHandler : IRequestHandler<UpsertKurinCommand, ServiceResult<KurinDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IBackendCache _cache;
    private readonly KurinNumberOptions _numbers;
    private readonly KraiOptions _krai;
    private readonly IKurinAudit _audit;
    private readonly IMembershipDirectory _memberships;

    /// <summary>What the kurin's journal calls these settings.</summary>
    public const string Label = "Дані куреня";

    public UpsertKurinCommandHandler(IUnitOfWork unitOfWork, IBlobReadLinks links, IBackendCache cache, IOptions<KurinNumberOptions> numbers, IOptions<KraiOptions> krai, IKurinAudit audit, IMembershipDirectory memberships)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _cache = cache;
        _numbers = numbers.Value;
        _krai = krai.Value;
        _audit = audit;
        _memberships = memberships;
    }
    public async Task<ServiceResult<KurinDto>> Handle(UpsertKurinCommand request, CancellationToken cancellationToken)
    {
        var tooLong = TooLongField(
            TextRules.TrimOrNull(request.Stanytsia),
            TextRules.TrimOrNull(request.RegionOrCountry),
            TextRules.TrimOrNull(request.NamedAfter),
            TextRules.TrimOrNull(request.Description));
        if (tooLong is not null)
        {
            return tooLong;
        }

        var existing = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);

        bool isCreated = false;

        var kraiCode = existing?.KraiCode ?? TextRules.TrimOrNull(request.KraiCode) ?? _krai.Default;
        if (existing is null && !await _unitOfWork.Krais.ExistsAsync(kraiCode, cancellationToken))
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.UnknownKrai, $"There is no край '{kraiCode}'.");
        }

        // A number is checked only when it changes: a kurin keeps the one it already has, so the
        // team's own kurins outside the range (a test kurin 0) can still be edited.
        if (await NumberRefusalAsync(existing, request.Number, kraiCode, cancellationToken) is { } numberRefused)
        {
            return numberRefused;
        }

        if (existing is null)
        {
            // Create new Kurin
            existing = new(request.Number) { KraiCode = kraiCode };
            _unitOfWork.Kurins.Create(existing, cancellationToken);
            isCreated = true;
        }
        else
        {
            // Update existing Kurin
            var before = Snapshot(existing);
            request.ApplyTo(existing, DateTime.UtcNow);

            // A УПЮ kurin keeps the usual names: an empty map from a client that sends the whole
            // record is no change, and only an actual name is refused.
            if (request.RoleNames is { } refusedNames && !KurinRoleNames.IsAvailableFor(existing.Branch)
                && refusedNames.Values.Any(name => !string.IsNullOrWhiteSpace(name)))
            {
                return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinRoleNames.NotAvailableCode, "This kurin keeps the usual role names.");
            }

            if (request.RoleNames is { } roleNames && KurinRoleNames.IsAvailableFor(existing.Branch))
            {
                var stored = KurinRoleNames.Normalize(roleNames, out var valid);
                if (!valid)
                {
                    return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinRoleNames.InvalidCode, $"A role name is one of the kurin's roles and up to {KurinRoleNames.MaxLength} characters.");
                }

                existing.RoleNames = stored;
            }

            _unitOfWork.Kurins.Update(existing, cancellationToken);

            var recorded = AuditChangeRules.Of(before.Zip(Snapshot(existing), (was, now) => (was.Field, was.Value, now.Value)).ToArray());
            if (recorded.Count > 0)
            {
                await _audit.RecordAsync(
                    new AuditRecord(AuditArea.Settings, AuditActionNames.Settings.KurinChanged, existing.KurinKey, Label, Changes: recorded, KurinKey: existing.KurinKey),
                    cancellationToken);
            }
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.InternalServerError, KurinErrors.KurinSaveFailed, "The kurin was not saved.");
        }

        var kurinResponse = existing.ToDto(_links, await _memberships.CountCurrentInKurinAsync(existing.KurinKey, cancellationToken));
        _cache.Invalidate(BackendCacheRules.KurinReads);
        _cache.Invalidate(BackendCacheRules.GroupReads);

        return isCreated
            ? new ServiceResult<KurinDto>(
                ResultType.Created,
                kurinResponse,
                CreatedAtActionName: "GetByKey",
                CreatedAtRouteValues: new { kurinKey = kurinResponse.KurinKey })
            : new ServiceResult<KurinDto>(ResultType.Success, kurinResponse);
    }

    private static ServiceResult<KurinDto>? TooLongField(string? stanytsia, string? regionOrCountry, string? namedAfter, string? description)
    {
        if (stanytsia?.Length > 120)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.StanytsiaTooLong, "Stanytsia must be 120 characters or fewer.");
        }

        if (regionOrCountry?.Length > 120)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.RegionOrCountryTooLong, "Region must be 120 characters or fewer.");
        }

        if (namedAfter?.Length > 200)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.NamedAfterTooLong, "Named after must be 200 characters or fewer.");
        }

        if (description?.Length > 4000)
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.DescriptionTooLong, "Description must be 4000 characters or fewer.");
        }

        return null;
    }

    private async Task<ServiceResult<KurinDto>?> NumberRefusalAsync(Common.Entities.KurinModule.Kurin? existing, int? requestedNumber, string kraiCode, CancellationToken cancellationToken)
    {
        var numberChanges = existing is null || existing.Number != requestedNumber;
        if (!numberChanges)
        {
            return null;
        }

        // A senior kurin's numbers are the administrators' to hand out: it asks through a request.
        if (existing is not null && KurinBranchSetRules.IsSenior(existing.Branch))
        {
            return ServiceResult<KurinDto>.Failure(ResultType.BadRequest, KurinErrors.SeniorNumberByRequest, "A senior kurin changes its numbers by request to an administrator.");
        }

        if (requestedNumber is not int number)
        {
            return null;
        }

        if (!_numbers.IsAllowed(number))
        {
            return ServiceResult<KurinDto>.Failure(
                ResultType.BadRequest,
                KurinErrors.KurinNumberOutOfRange,
                _numbers.OutOfRangeMessage);
        }

        var existingByNumber = await _unitOfWork.Kurins.GetByNumberAsync(kraiCode, number, cancellationToken, existing?.Branch ?? KurinBranch.UPYu);
        if (existingByNumber is not null)
        {
            return existing is null
                ? new ServiceResult<KurinDto>(ResultType.Conflict, existingByNumber.ToDto(_links, await _memberships.CountCurrentInKurinAsync(existingByNumber.KurinKey, cancellationToken)))
                : ServiceResult<KurinDto>.Failure(ResultType.Conflict, KurinErrors.KurinNumberTaken, "A kurin with this number already exists in this край.");
        }

        return null;
    }

    /// <summary>The kurin's details the journal records when they change.</summary>
    private static (string Field, object? Value)[] Snapshot(Common.Entities.KurinModule.Kurin kurin) =>
    [
        ("number", kurin.Number),
        ("stanytsia", kurin.Stanytsia),
        ("regionOrCountry", kurin.RegionOrCountry),
        ("namedAfter", kurin.NamedAfter),
        ("description", kurin.Description),
        ("profileVerificationEnabled", kurin.ProfileVerificationEnabled),
        ("scoreEnabled", kurin.IsScoreEnabled),
        ("roleNames", kurin.RoleNames)
    ];
}
