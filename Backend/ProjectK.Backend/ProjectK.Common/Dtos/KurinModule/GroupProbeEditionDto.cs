namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A гурток with the edition of the probe programme it walks and where each youth stands.</summary>
/// <param name="GroupKey">The гурток.</param>
/// <param name="Name">Its name.</param>
/// <param name="Edition">The edition the гурток walks: null for the unnamed one, and also when the
/// one it chose is gone from the catalogue — then its youths are back on the unnamed one.</param>
/// <param name="CanChange">Whether the caller may switch it: the kurin's «Обирати пробу гуртку».</param>
/// <param name="Members">Its youths, each with what decides their edition.</param>
public sealed record GroupProbeEditionDto(
    Guid GroupKey,
    string Name,
    string? Edition,
    bool CanChange,
    IReadOnlyList<GroupMemberStandingDto> Members);
