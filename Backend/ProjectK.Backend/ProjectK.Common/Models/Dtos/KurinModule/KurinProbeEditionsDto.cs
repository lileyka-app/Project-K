namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>
/// The probe edition of every гурток in a kurin, for the kurin's settings: the editions the
/// catalogue offers its край and, per гурток, where each youth stands.
/// </summary>
public sealed record KurinProbeEditionsDto(
    IReadOnlyList<ProbeEditionDto> Editions,
    IReadOnlyList<GroupProbeEditionDto> Groups);
