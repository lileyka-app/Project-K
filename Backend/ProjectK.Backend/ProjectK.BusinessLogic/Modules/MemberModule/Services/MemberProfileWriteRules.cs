using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;
using MemberEntity = ProjectK.Common.Entities.MemberModule.Member;
using MembershipEntity = ProjectK.Common.Entities.MemberModule.Membership;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// What a profile write does to the values it carries before they are stored: the shape the phones
/// are kept in, and whether the change is big enough to send a verified profile back for a check.
/// </summary>
public static class MemberProfileWriteRules
{
    /// <summary>
    /// Puts the member's and the parents' phones into the stored shape, in place. False when one of
    /// them is not a number the rule accepts.
    /// </summary>
    public static bool TryNormalizePhones(UpsertMemberProfileCommand request, MemberEntity? existing)
    {
        // Every write stores the phone as +380501234567. One the member already has is let through
        // as it is, so a number older than the rule does not block an edit of something else.
        if (existing is null || !string.Equals(request.PhoneNumber, existing.PhoneNumber, StringComparison.Ordinal))
        {
            var phone = PhoneNumberRules.Normalize(request.PhoneNumber);
            if (phone is null)
            {
                return false;
            }

            request.PhoneNumber = phone;
        }

        // A parent's phone is stored in the same shape; an empty one stays empty.
        if (!TryNormalizeParentPhone(request.MotherPhone, existing?.MotherPhone, out var motherPhone)
            || !TryNormalizeParentPhone(request.FatherPhone, existing?.FatherPhone, out var fatherPhone))
        {
            return false;
        }

        request.MotherPhone = motherPhone;
        request.FatherPhone = fatherPhone;
        return true;
    }

    /// <summary>
    /// True when the write changes who the person is, how they are reached, where they stand, or —
    /// when the writer may change them — their ступені.
    /// </summary>
    public static bool IsSignificantChange(
        UpsertMemberProfileCommand request,
        MemberEntity existing,
        MembershipEntity? current,
        Guid targetKurinKey,
        Guid? targetGroupKey,
        bool plastLevelsCount)
    {
        return !string.Equals(existing.FirstName, request.FirstName, StringComparison.Ordinal)
               || !string.Equals(existing.MiddleName ?? string.Empty, request.MiddleName ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existing.LastName, request.LastName, StringComparison.Ordinal)
               || !string.Equals(existing.Email, request.Email, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(existing.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal)
               || existing.DateOfBirth != request.DateOfBirth
               || !string.Equals(existing.Address ?? string.Empty, request.Address ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existing.School ?? string.Empty, request.School ?? string.Empty, StringComparison.Ordinal)
               || existing.ParentsDiffer(request)
               || current?.GroupKey != targetGroupKey
               || current?.KurinKey != targetKurinKey
               || (plastLevelsCount && PlastLevelHistoryRules.Differs(request.PlastLevelHistories, existing.PlastLevelHistory));
    }

    private static bool TryNormalizeParentPhone(string? requested, string? stored, out string? phone)
    {
        phone = requested;
        if (string.IsNullOrWhiteSpace(requested) || string.Equals(requested, stored, StringComparison.Ordinal))
        {
            return true;
        }

        phone = PhoneNumberRules.Normalize(requested);
        return phone is not null;
    }
}
