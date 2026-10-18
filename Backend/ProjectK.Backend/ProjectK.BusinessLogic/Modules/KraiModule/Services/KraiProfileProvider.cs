using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Services;

/// <summary>
/// Builds a край's profile: the values in code first, the край's latest saved revision over them.
/// The values in code are how Lilejka worked before profiles, so an empty profile changes nothing.
/// </summary>
public sealed class KraiProfileProvider : IKraiProfileProvider
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    private readonly ILogger<KraiProfileProvider> _logger;

    public KraiProfileProvider(IUnitOfWork unitOfWork, IBackendCache cache, ILogger<KraiProfileProvider> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public Task<KraiProfile?> GetForKraiAsync(string kraiCode, CancellationToken cancellationToken = default)
    {
        return _cache.GetOrCreateAsync(
            BackendCachePolicies.KraiProfileReads,
            kraiCode,
            token => BuildAsync(kraiCode, token),
            cancellationToken);
    }

    public async Task<KraiProfile?> GetForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        if (kurin is null || await GetForKraiAsync(kurin.KraiCode, cancellationToken) is not { } profile)
        {
            return null;
        }

        // The край's profile is shared and cached; what the kurin leaves off its card is its own.
        var hidden = KurinHiddenCardFields.Read(kurin.HiddenCardFields);
        return hidden.Count == 0 ? profile : profile with { CardFields = profile.CardFields.HidingFor(hidden) };
    }

    private async Task<KraiProfile?> BuildAsync(string kraiCode, CancellationToken cancellationToken)
    {
        var krai = await _unitOfWork.Krais.GetAsync(kraiCode, cancellationToken);
        if (krai is null)
        {
            return null;
        }

        var revision = await _unitOfWork.Krais.GetLatestProfileRevisionAsync(kraiCode, cancellationToken);
        return Resolve(krai, revision, Read(revision));
    }

    private KraiProfileDocument Read(KraiProfileRevision? revision)
    {
        if (revision is null)
        {
            return new KraiProfileDocument();
        }

        // A revision newer than this Lilejka understands is still read: fields it does not know
        // are skipped, the ones it knows keep their meaning. A schema change that renames or
        // reinterprets a field adds its conversion here, keyed on SchemaVersion.
        try
        {
            return KraiProfileJson.Parse(revision.Json);
        }
        catch (JsonException exception)
        {
            // A broken profile must not take the kurins of its край down with it; they work on
            // the values in code until an admin saves a good one.
            _logger.LogError(exception, "Profile revision {Revision} of край {Krai} is not valid JSON; using the defaults.", revision.Revision, revision.KraiCode);
            return new KraiProfileDocument();
        }
    }

    private static KraiProfile Resolve(Krai krai, KraiProfileRevision? revision, KraiProfileDocument document)
    {
        var catalog = document.Catalog;
        var locale = document.Locale;

        return new KraiProfile(
            krai.Code,
            krai.Name,
            revision?.Revision ?? 0,
            new KraiCatalogSettings(
                BadgeSources: Codes(catalog?.BadgeSources) ?? [krai.Code],
                HiddenBadges: Ids(catalog?.HiddenBadges)),
            new KraiLocaleSettings(
                Currency: Text(locale?.Currency)?.ToUpperInvariant() ?? "UAH",
                TimeZone: Text(locale?.TimeZone) ?? AgendaRecurrence.DefaultZone.Id,
                NumberLocale: Text(locale?.NumberLocale) ?? "uk-UA"),
            CardFields(document.CardFields),
            new KraiDuesSettings(Text(document.Dues?.Upstream)?.ToLowerInvariant() is { } upstream && KraiDues.Upstreams.Contains(upstream)
                ? upstream
                : KraiDues.Stanytsia));
    }

    // The card as it was before profiles, with whatever the край changed over it. A mode or a
    // field this Lilejka does not know is left out rather than guessed at.
    private static KraiCardFieldsSettings CardFields(KraiCardFieldsDocument? document)
    {
        var fields = KraiCardFields.Configurable.ToDictionary(
            field => field.Key,
            field => document?.Fields is { } set
                && set.TryGetValue(field.Key, out var mode)
                && KraiCardFields.Modes.Contains(mode)
                    ? mode
                    : field.Value);

        var extra = (document?.Extra ?? [])
            .Where(field => !string.IsNullOrWhiteSpace(field.Key)
                && !string.IsNullOrWhiteSpace(field.Label)
                && KraiCardFields.Types.Contains(field.Type))
            .DistinctBy(field => field.Key)
            .ToList();

        return new KraiCardFieldsSettings(fields, extra);
    }

    // An empty list of sources is read as "not set", never as "show nothing".
    private static IReadOnlyList<string>? Codes(IReadOnlyList<string>? codes)
    {
        var cleaned = codes?
            .Select(Text)
            .OfType<string>()
            .Select(code => code.ToLowerInvariant())
            .Distinct()
            .ToList();
        return cleaned is { Count: > 0 } ? cleaned : null;
    }

    private static IReadOnlyList<string> Ids(IReadOnlyList<string>? ids)
    {
        return ids?.Select(Text).OfType<string>().Distinct().ToList() ?? [];
    }

    private static string? Text(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
