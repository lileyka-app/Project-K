using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Services;

/// <summary>How a край's profile document is read, checked and written, in one place.</summary>
public static partial class KraiProfileJson
{
    /// <summary>A profile is a handful of lists; anything near this size is a mistake.</summary>
    public const int MaxLength = 64 * 1024;

    private const int MaxListItems = 2000;
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
                Currency = Text(locale.Currency)?.ToUpperInvariant(),
                TimeZone = Text(locale.TimeZone),
                NumberLocale = Text(locale.NumberLocale)
            }),
            CardFields = card is null ? null : Empty(new KraiCardFieldsDocument
            {
                Fields = Modes(card.Fields),
                Extra = Extra(card.Extra)
            }),
            Dues = dues is null ? null : Empty(new KraiDuesDocument
            {
                Upstream = Text(dues.Upstream)?.ToLowerInvariant()
            })
        };
    }

    /// <summary>What is wrong with a normalized document, or nothing.</summary>
    public static IReadOnlyList<string> Validate(KraiProfileDocument document)
    {
        var errors = new List<string>();

        if (document.Catalog?.BadgeSources is { } sources)
        {
            errors.AddRange(sources.Where(code => !KraiCodePattern().IsMatch(code)).Select(code => $"Невідомий код краю: «{code}»."));
        }

        if ((document.Catalog?.BadgeSources?.Count ?? 0) > MaxListItems || (document.Catalog?.HiddenBadges?.Count ?? 0) > MaxListItems)
        {
            errors.Add("Список завеликий.");
        }

        if (document.Catalog?.HiddenBadges?.Any(id => id.Length > 200) == true)
        {
            errors.Add("Задовгий ідентифікатор вмілості.");
        }

        if (document.Locale?.Currency is { } currency && !CurrencyPattern().IsMatch(currency))
        {
            errors.Add($"Валюта має бути кодом ISO 4217, як UAH чи CZK: «{currency}».");
        }

        if (document.Locale?.TimeZone is { } zone && !IsTimeZone(zone))
        {
            errors.Add($"Невідомий часовий пояс: «{zone}».");
        }

        if (document.Locale?.NumberLocale is { } culture && !IsCulture(culture))
        {
            errors.Add($"Невідомий формат чисел: «{culture}».");
        }

        if (document.CardFields is { } card)
        {
            errors.AddRange(CardFieldErrors(card));
        }

        if (document.Dues?.Upstream is { } upstream && !KraiDues.Upstreams.Contains(upstream))
        {
            errors.Add($"Вкладку можна передавати лише stanytsia, krai чи both, а не «{upstream}».");
        }

        return errors;
    }

    private static IEnumerable<string> CardFieldErrors(KraiCardFieldsDocument card)
    {
        foreach (var (field, mode) in card.Fields ?? new Dictionary<string, string>())
        {
            if (!KraiCardFields.Configurable.ContainsKey(field))
            {
                yield return $"Поле «{field}» картки не налаштовується.";
            }
            else if (!KraiCardFields.Modes.Contains(mode))
            {
                yield return $"Поле «{field}» може бути лише required, optional чи hidden, а не «{mode}».";
            }
        }

        var extra = card.Extra ?? [];
        if (extra.Count > MaxExtraFields)
        {
            yield return $"Додаткових полів може бути щонайбільше {MaxExtraFields}.";
        }

        foreach (var field in extra)
        {
            if (!ExtraKeyPattern().IsMatch(field.Key))
            {
                yield return $"Ключ поля «{field.Key}» має бути латиницею, з малої літери, без пробілів.";
            }

            if (string.IsNullOrEmpty(field.Label) || field.Label.Length > MaxLabelLength)
            {
                yield return $"Поле «{field.Key}» потребує назви до {MaxLabelLength} знаків.";
            }

            if (!KraiCardFields.Types.Contains(field.Type))
            {
                yield return $"Поле «{field.Key}» може бути лише text чи date, а не «{field.Type}».";
            }
        }

        foreach (var key in extra.GroupBy(field => field.Key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            yield return $"Ключ поля «{key}» повторюється.";
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
                Type = Text(field.Type)?.ToLowerInvariant() ?? KraiCardFields.Text,
                Required = field.Required
            })
            .ToList();
        return list is { Count: > 0 } ? list : null;
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

    private static string? Text(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
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

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
