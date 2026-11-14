using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Dtos.MemberModule;

/// <param name="Give">The levels (1–3) the caller may give.</param>
/// <param name="Cancel">Whose перестороги the caller may cancel.</param>
public sealed record WarningAbilitiesDto(IReadOnlyList<int> Give, WarningCancelAbility Cancel);
