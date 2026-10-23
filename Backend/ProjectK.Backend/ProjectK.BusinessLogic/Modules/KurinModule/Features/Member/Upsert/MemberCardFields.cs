using ProjectK.Common.Models.Members;
using System.Globalization;
using ProjectK.Common.Models.KraiProfiles;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;

/// <summary>
/// What a край's card fields do to a write from the form: a hidden field keeps what it had, a
/// required one must be filled, and the край's own fields are merged into what the member has.
/// A group of data the writer may not see is not asked for: they could not have filled it.
/// </summary>
public static class MemberCardFields
{
    public const string ErrorCode = "CardFieldRequired";

    private const int MaxValueLength = 200;

    /// <summary>The member's values of the край's own fields once this write lands.</summary>
    public sealed record Outcome(string? CardExtras, string? Error);

    public static Outcome Apply(
        KraiCardFieldsSettings card,
        UpsertMemberProfileCommand request,
        MemberEntity? existing,
        IReadOnlySet<MemberDataGroup>? hiddenFromWriter = null)
    {
        hiddenFromWriter ??= new HashSet<MemberDataGroup>();
        KeepWhatTheFormDidNotSend(card, request, existing);

        var error = TooLongField(request) ?? MissingRequiredField(card, request, hiddenFromWriter);
        if (error is not null)
        {
            return new Outcome(existing?.CardExtras, error);
        }

        var values = MemberCardExtras.Read(existing?.CardExtras);
        error = MergeExtras(card, request, values, hiddenFromWriter.Contains(MemberDataGroup.Extras));
        return error is null
            ? new Outcome(MemberCardExtras.Write(values), null)
            : new Outcome(existing?.CardExtras, error);
    }

    private static void KeepWhatTheFormDidNotSend(KraiCardFieldsSettings card, UpsertMemberProfileCommand request, MemberEntity? existing)
    {
        if (card.ModeOf(KraiCardFields.MiddleName) == KraiCardFields.Hidden)
        {
            request.MiddleName = existing?.MiddleName ?? string.Empty;
        }

        if (card.ModeOf(KraiCardFields.PhoneNumber) == KraiCardFields.Hidden)
        {
            request.PhoneNumber = existing?.PhoneNumber ?? string.Empty;
        }

        // An optional field left empty arrives as null; the phone column takes no nulls.
        request.PhoneNumber ??= string.Empty;

        // Address and school are private: a form that never showed them sends nothing, and that keeps
        // what is stored. An empty value clears it.
        if (card.ModeOf(KraiCardFields.Address) == KraiCardFields.Hidden || request.Address is null)
        {
            request.Address = existing?.Address;
        }

        if (card.ModeOf(KraiCardFields.School) == KraiCardFields.Hidden || request.School is null)
        {
            request.School = existing?.School;
        }

        request.Address = EmptyAsNull(request.Address);
        request.School = EmptyAsNull(request.School);

        // Parents' contacts are private the same way, each value on its own.
        var parentsHidden = card.ModeOf(KraiCardFields.Parents) == KraiCardFields.Hidden;
        foreach (var field in ParentContacts.Fields)
        {
            var sent = field.Get(request);
            var kept = existing is null ? null : field.Get(existing);
            field.Set(request, EmptyAsNull(parentsHidden || sent is null ? kept : sent));
        }
    }

    private static string? TooLongField(UpsertMemberProfileCommand request)
    {
        var parentNames = new[] { request.MotherFirstName, request.MotherLastName, request.FatherFirstName, request.FatherLastName };
        if (parentNames.Any(name => name?.Length > MaxValueLength))
        {
            return $"Поле «{KraiCardFields.Labels[KraiCardFields.Parents]}» задовге.";
        }

        if (request.Address?.Length > MaxValueLength || request.School?.Length > MaxValueLength)
        {
            var field = request.Address?.Length > MaxValueLength ? KraiCardFields.Address : KraiCardFields.School;
            return $"Поле «{KraiCardFields.Labels[field]}» задовге.";
        }

        return null;
    }

    private static string? MissingRequiredField(
        KraiCardFieldsSettings card,
        UpsertMemberProfileCommand request,
        IReadOnlySet<MemberDataGroup> hiddenFromWriter)
    {
        var missing = new[]
            {
                (Field: KraiCardFields.MiddleName, Value: request.MiddleName, Group: (MemberDataGroup?)null),
                (Field: KraiCardFields.PhoneNumber, Value: request.PhoneNumber, Group: MemberDataGroup.Contacts),
                (Field: KraiCardFields.Address, Value: request.Address, Group: MemberDataGroup.Address),
                (Field: KraiCardFields.School, Value: request.School, Group: MemberDataGroup.Address)
            }
            .Where(field => field.Group is not { } group || !hiddenFromWriter.Contains(group))
            .FirstOrDefault(field => card.ModeOf(field.Field) == KraiCardFields.Required && string.IsNullOrWhiteSpace(field.Value));

        // Required parents' contacts mean at least one parent the kurin can call.
        if (missing.Field is null && card.ModeOf(KraiCardFields.Parents) == KraiCardFields.Required
            && !hiddenFromWriter.Contains(MemberDataGroup.Parents)
            && request.MotherPhone is null && request.FatherPhone is null)
        {
            missing.Field = KraiCardFields.Parents;
        }

        return missing.Field is null ? null : $"Поле «{KraiCardFields.Labels[missing.Field]}» обовʼязкове.";
    }

    private static string? MergeExtras(
        KraiCardFieldsSettings card,
        UpsertMemberProfileCommand request,
        Dictionary<string, string> values,
        bool hiddenFromWriter)
    {
        foreach (var field in card.Extra)
        {
            if (request.CardExtras?.TryGetValue(field.Key, out var sent) == true)
            {
                var value = sent?.Trim() ?? string.Empty;
                if (value.Length > MaxValueLength)
                {
                    return $"Поле «{field.Label}» задовге.";
                }

                if (value.Length > 0 && field.Type == KraiCardFields.Date
                    && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    return $"Поле «{field.Label}» має бути датою.";
                }

                if (value.Length == 0)
                {
                    values.Remove(field.Key);
                }
                else
                {
                    values[field.Key] = value;
                }
            }

            if (field.Required && !hiddenFromWriter && !values.ContainsKey(field.Key))
            {
                return $"Поле «{field.Label}» обовʼязкове.";
            }
        }

        return null;
    }

    private static string? EmptyAsNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
