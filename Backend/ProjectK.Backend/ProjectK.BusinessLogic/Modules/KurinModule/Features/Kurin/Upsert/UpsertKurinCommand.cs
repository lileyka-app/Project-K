using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;

public class UpsertKurinCommand : IRequest<ServiceResult<KurinResponse>>
{
    public Guid KurinKey { get; set; }

    /// <summary>The kurin's number; null makes it a підготовчий курінь.</summary>
    public int? Number { get; set; }
    public string? Stanytsia { get; set; }
    public string? RegionOrCountry { get; set; }
    public string? NamedAfter { get; set; }
    public string? Description { get; set; }
    public bool ProfileVerificationEnabled { get; set; }

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

public class UpsertKurinCommandHandler : IRequestHandler<UpsertKurinCommand, ServiceResult<KurinResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IBackendCache _cache;
    private readonly KurinNumberOptions _numbers;
    private readonly KraiOptions _krai;
    private readonly IKurinAudit _audit;

    /// <summary>What the kurin's journal calls these settings.</summary>
    public const string Label = "Дані куреня";

    public UpsertKurinCommandHandler(IUnitOfWork unitOfWork, IMapper mapper, IBackendCache cache, IOptions<KurinNumberOptions> numbers, IOptions<KraiOptions> krai, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _cache = cache;
        _numbers = numbers.Value;
        _krai = krai.Value;
        _audit = audit;
    }
    public async Task<ServiceResult<KurinResponse>> Handle(UpsertKurinCommand request, CancellationToken cancellationToken)
    {
        var stanytsia = NormalizeOptionalText(request.Stanytsia);
        var regionOrCountry = NormalizeOptionalText(request.RegionOrCountry);
        var namedAfter = NormalizeOptionalText(request.NamedAfter);
        var description = NormalizeOptionalText(request.Description);

        if (TooLongField(stanytsia, regionOrCountry, namedAfter, description) is { } tooLong)
        {
            return tooLong;
        }

        var existing = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);

        bool isCreated = false;

        var kraiCode = existing?.KraiCode ?? NormalizeOptionalText(request.KraiCode) ?? _krai.Default;
        if (existing is null && !await _unitOfWork.Krais.ExistsAsync(kraiCode, cancellationToken))
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "UnknownKrai", $"There is no край '{kraiCode}'.");
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
            _mapper.Map(request, existing);
            existing.Number = request.Number;
            existing.Stanytsia = stanytsia;
            existing.RegionOrCountry = regionOrCountry;
            existing.NamedAfter = namedAfter;
            existing.Description = description;
            existing.ProfileVerificationEnabled = request.ProfileVerificationEnabled;
            _unitOfWork.Kurins.Update(existing, cancellationToken);

            var recorded = AuditChanges.Of(before.Zip(Snapshot(existing), (was, now) => (was.Field, was.Value, now.Value)).ToArray());
            if (recorded.Count > 0)
            {
                await _audit.RecordAsync(
                    new AuditRecord(AuditArea.Settings, AuditActions.Settings.KurinChanged, existing.KurinKey, Label, Changes: recorded, KurinKey: existing.KurinKey),
                    cancellationToken);
            }
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return new ServiceResult<KurinResponse>(
                ResultType.InternalServerError);
        }

        var kurinResponse = _mapper.Map<KurinResponse>(existing);
        _cache.Invalidate(BackendCachePolicies.KurinReads);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return isCreated
            ? new ServiceResult<KurinResponse>(
                ResultType.Created,
                kurinResponse,
                CreatedAtActionName: "GetByKey",
                CreatedAtRouteValues: new { kurinKey = kurinResponse.KurinKey })
            : new ServiceResult<KurinResponse>(ResultType.Success, kurinResponse);
    }

    private static ServiceResult<KurinResponse>? TooLongField(string? stanytsia, string? regionOrCountry, string? namedAfter, string? description)
    {
        if (stanytsia?.Length > 120)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "StanytsiaTooLong", "Stanytsia must be 120 characters or fewer.");
        }

        if (regionOrCountry?.Length > 120)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "RegionOrCountryTooLong", "Region must be 120 characters or fewer.");
        }

        if (namedAfter?.Length > 200)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "NamedAfterTooLong", "Named after must be 200 characters or fewer.");
        }

        if (description?.Length > 4000)
        {
            return ServiceResult<KurinResponse>.Failure(ResultType.BadRequest, "DescriptionTooLong", "Description must be 4000 characters or fewer.");
        }

        return null;
    }

    private async Task<ServiceResult<KurinResponse>?> NumberRefusalAsync(Common.Entities.KurinModule.Kurin? existing, int? requestedNumber, string kraiCode, CancellationToken cancellationToken)
    {
        var numberChanges = existing is null || existing.Number != requestedNumber;
        if (!numberChanges || requestedNumber is not int number)
        {
            return null;
        }

        if (!_numbers.IsAllowed(number))
        {
            return ServiceResult<KurinResponse>.Failure(
                ResultType.BadRequest,
                "KurinNumberOutOfRange",
                $"Kurin number must be between {KurinNumberOptions.MinNumber} and {_numbers.MaxNumber}.");
        }

        var existingByNumber = await _unitOfWork.Kurins.GetByNumberAsync(kraiCode, number, cancellationToken);
        if (existingByNumber is not null)
        {
            return existing is null
                ? new ServiceResult<KurinResponse>(ResultType.Conflict, _mapper.Map<KurinResponse>(existingByNumber))
                : ServiceResult<KurinResponse>.Failure(ResultType.Conflict, "KurinNumberTaken", "A kurin with this number already exists in this край.");
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
        ("profileVerificationEnabled", kurin.ProfileVerificationEnabled)
    ];

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
