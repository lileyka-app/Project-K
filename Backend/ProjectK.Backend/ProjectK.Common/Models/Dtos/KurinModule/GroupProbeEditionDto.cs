namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <param name="Edition">The edition the гурток walks: null for the unnamed one, and also when the
/// one it chose is gone from the catalogue — then its youths are back on the unnamed one.</param>
/// <param name="CanChange">Whether the caller may switch it: the kurin's «Обирати пробу гуртку».</param>
public sealed record GroupProbeEditionDto(
    Guid GroupKey,
    string Name,
    string? Edition,
    bool CanChange,
    IReadOnlyList<GroupMemberStandingDto> Members);
