using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Rules.KraiModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Services;

/// <summary>How a край's profile document is read, checked and written, in one place.</summary>
public static partial class KraiProfileJsonRules
{
    /// <summary>A profile is a handful of lists; anything near this size is a mistake.</summary>
    public const int MaxLength = 64 * 1024;

    private const int MaxListItems = 2000;
    private const int MaxConcessions = 50;
    private const int MaxExtraFields = 30;
    private const int MaxLabelLength = 80;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Reads a stored document; throws <see cref="JsonException"/> when it is not one.</summary>
    public static KraiProfileDocument Parse(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? new KraiProfileDocument()
            : JsonSerializer.Deserialize<KraiProfileDocument>(json, ReadOptions) ?? new KraiProfileDocument();
    }

    public static string Serialize(KraiProfileDocument document)
    {
        return JsonSerializer.Serialize(document, WriteOptions);
    }

    /// <summary>
    /// The document as it is saved: codes lower-cased, blanks and repeats dropped, empty lists
    /// left out so they keep meaning "not set".
    /// </summary>
    public static KraiProfileDocument Normalize(KraiProfileDocument document)
    {
        var catalog = document.Catalog;
        var locale = document.Locale;
        var card = document.CardFields;
        var dues = document.Dues;

        return new KraiProfileDocument
        {
            Catalog = catalog is null ? null : Empty(new KraiCatalogDocument
            {
                BadgeSources = List(catalog.BadgeSources?.Select(code => code?.Trim().ToLowerInvariant())),
                HiddenBadges = List(catalog.HiddenBadges?.Select(id => id?.Trim()))
            }),
            Locale = locale is null ? null : Empty(new KraiLocaleDocument
            {
                Currency = TextRules.TrimOrNull(locale.Currency)?.ToUpperInvariant(),
                TimeZone = TextRules.TrimOrNull(locale.TimeZone),
                NumberLocale = TextRules.TrimOrNull(locale.NumberLocale)
            }),
            CardFields = card is null ? null : Empty(new KraiCardFieldsDocument
            {
                Fields = Modes(card.Fields),
                Extra = Extra(card.Extra)
            }),
            Dues = dues is null ? null : Empty(new KraiDuesDocument
            {
                Upstream = TextRules.TrimOrNull(dues.Upstream)?.ToLowerInvariant(),
                Concessions = Concessions(dues.Concessions)
            })
        };
    }

    /// <summary>What is wrong with a normalized document, or nothing.</summary>
    /// <summary>Whether <paramref name="code"/> can name a край: 2 to 8 lowercase latin letters.</summary>
    public static bool IsKraiCode(string code) => KraiCodePattern().IsMatch(code);

    public static IReadOnlyList<string> Validate(KraiProfileDocument document)
    {
        var errors = new List<string>();

        if (document.Catalog?.BadgeSources is { } sources)
        {
            errors.AddRange(sources.Where(code => !KraiCodePattern().IsMatch(code)).Select(code => $"Unknown krai code '{code}'."));
        }

        if ((document.Catalog?.BadgeSources?.Count ?? 0) > MaxListItems || (document.Catalog?.HiddenBadges?.Count ?? 0) > MaxListItems)
        {
            errors.Add("A list is too long.");
        }

        if (document.Catalog?.HiddenBadges?.Any(id => id.Length > 200) == true)
        {
            errors.Add("A badge id is too long.");
        }

        if (document.Locale?.Currency is { } currency && !CurrencyPattern().IsMatch(currency))
        {
            errors.Add($"The currency must be an ISO 4217 code like UAH or CZK, not '{currency}'.");
        }

        if (document.Locale?.TimeZone is { } zone && !IsTimeZone(zone))
        {
            errors.Add($"Unknown time zone '{zone}'.");
        }

        if (document.Locale?.NumberLocale is { } culture && !IsCulture(culture))
        {
            errors.Add($"Unknown number locale '{culture}'.");
        }

        if (document.CardFields is { } card)
        {
            errors.AddRange(CardFieldErrors(card));
        }

        if (document.Dues?.Upstream is { } upstream && !KraiDues.Upstreams.Contains(upstream))
        {
            errors.Add($"Dues go upstream to stanytsia, krai or both, not '{upstream}'.");
        }

        if (document.Dues?.Concessions is { } concessions)
        {
            errors.AddRange(ConcessionErrors(concessions));
        }

        return errors;
    }

    private static IEnumerable<string> CardFieldErrors(KraiCardFieldsDocument card)
    {
        foreach (var (field, mode) in card.Fields ?? new Dictionary<string, string>())
        {
            if (!KraiCardFields.Configurable.ContainsKey(field))
            {
                yield return $"Card field '{field}' is not configurable.";
            }
            else if (!KraiCardFields.Modes.Contains(mode))
            {
                yield return $"Card field '{field}' is required, optional or hidden, not '{mode}'.";
            }
        }

        var extra = card.Extra ?? [];
        if (extra.Count > MaxExtraFields)
        {
            yield return $"At most {MaxExtraFields} extra fields are allowed.";
        }

        foreach (var field in extra)
        {
            if (!ExtraKeyPattern().IsMatch(field.Key))
            {
                yield return $"Field key '{field.Key}' must be latin, start lowercase and have no spaces.";
            }

            if (string.IsNullOrEmpty(field.Label) || field.Label.Length > MaxLabelLength)
            {
                yield return $"Field '{field.Key}' needs a label of 1 to {MaxLabelLength} characters.";
            }

            if (!KraiCardFields.Types.Contains(field.Type))
            {
                yield return $"Field '{field.Key}' is text or date, not '{field.Type}'.";
            }
        }

        foreach (var key in extra.GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            yield return $"Field key '{key}' is repeated.";
        }
    }

    private static IReadOnlyDictionary<string, string>? Modes(IReadOnlyDictionary<string, string>? fields)
    {
        var modes = fields?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
        return modes is { Count: > 0 } ? modes : null;
    }

    private static IReadOnlyList<KraiCardExtraField>? Extra(IReadOnlyList<KraiCardExtraField>? fields)
    {
        var list = fields?
            .Where(field => field is not null && !string.IsNullOrWhiteSpace(field.Key))
            .Select(field => new KraiCardExtraField
            {
                Key = field.Key.Trim(),
                Label = field.Label?.Trim() ?? string.Empty,
                Type = TextRules.TrimOrNull(field.Type)?.ToLowerInvariant() ?? KraiCardFields.Text,
                Required = field.Required
            })
            .ToList();
        return list is { Count: > 0 } ? list : null;
    }

    private static IReadOnlyList<KraiConcessionDocument>? Concessions(IReadOnlyList<KraiConcessionDocument>? concessions)
    {
        var list = concessions?
            .Where(c => c is not null)
            .Select(c => new KraiConcessionDocument
            {
                Key = c.Key?.Trim() ?? string.Empty,
                Name = c.Name?.Trim() ?? string.Empty,
                Kind = c.Kind?.Trim().ToLowerInvariant() ?? string.Empty,
                Value = c.Kind?.Trim().ToLowerInvariant() == KraiDues.Exempt ? null : c.Value,
                AppliesTo = List(c.AppliesTo?.Select(part => part?.Trim().ToLowerInvariant()))
            })
            .ToList();
        return list is { Count: > 0 } ? list : null;
    }

    private static IEnumerable<string> ConcessionErrors(IReadOnlyList<KraiConcessionDocument> concessions)
    {
        if (concessions.Count > MaxConcessions)
        {
            yield return "There are too many concessions.";
        }

        foreach (var key in concessions.GroupBy(c => c.Key).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            yield return $"Two concessions share the key '{key}'.";
        }

        foreach (var concession in concessions)
        {
            var name = string.IsNullOrEmpty(concession.Name) ? concession.Key : concession.Name;
            if (!ConcessionKeyPattern().IsMatch(concession.Key))
            {
                yield return $"Concession key of '{name}' is latin letters, digits and hyphens, up to 40 characters.";
            }

            if (concession.Name.Length is 0 or > 80)
            {
                yield return $"Concession '{concession.Key}' needs a name of 1 to 80 characters.";
            }

            if (!KraiDues.ConcessionKinds.Contains(concession.Kind))
            {
                yield return $"Concession '{name}' is percent, amount or exempt, not '{concession.Kind}'.";
            }
            else if (concession.Kind == KraiDues.Percent && concession.Value is not (> 0 and <= 100))
            {
                yield return $"Concession '{name}' takes off more than 0 and at most 100 percent.";
            }
            else if (concession.Kind == KraiDues.Amount && concession.Value is not > 0)
            {
                yield return $"Concession '{name}' must take off more than zero.";
            }

            if (concession.AppliesTo is not { Count: > 0 } parts)
            {
                yield return $"Concession '{name}' must apply to at least one part of the dues.";
            }
            else
            {
                foreach (var part in parts.Where(part => !KraiDues.Parts.Contains(part)))
                {
                    yield return $"Concession '{name}' names an unknown part of the dues '{part}'.";
                }
            }
        }
    }

    private static T? Empty<T>(T section) where T : class
    {
        // A section with nothing set is the same as no section.
        return JsonSerializer.Serialize(section, WriteOptions) == "{}" ? null : section;
    }

    private static IReadOnlyList<string>? List(IEnumerable<string?>? values)
    {
        var list = values?.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct(StringComparer.Ordinal).ToList();
        return list is { Count: > 0 } ? list : null;
    }

    private static bool IsTimeZone(string id)
    {
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
    }

    private static bool IsCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name.Length > 0;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z]{2,8}$")]
    private static partial Regex KraiCodePattern();

    [GeneratedRegex("^[a-z][a-zA-Z0-9]{0,39}$")]
    private static partial Regex ExtraKeyPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex ConcessionKeyPattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
