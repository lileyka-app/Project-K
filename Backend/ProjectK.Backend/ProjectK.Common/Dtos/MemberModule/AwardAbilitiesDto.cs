namespace ProjectK.Common.Dtos.MemberModule;

/// <param name="Confirm">The levels (1–4) the caller may confirm.</param>
/// <param name="Remove">Whether the caller may remove a confirmed award or someone else's submission.</param>
public sealed record AwardAbilitiesDto(IReadOnlyList<int> Confirm, bool Remove);
