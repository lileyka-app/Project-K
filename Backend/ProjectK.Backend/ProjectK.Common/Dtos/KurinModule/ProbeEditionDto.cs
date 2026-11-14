namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>One edition of the probe programme the catalogue offers.</summary>
/// <param name="Id">The catalogue id of the edition, e.g. "upyu-2026"; null for the unnamed one.</param>
/// <param name="Title">The name the catalogue gives the edition; null for the unnamed one, which the
/// clients call «Звичайна проба» themselves.</param>
/// <param name="IsTrial">The catalogue still marks the edition as in апробація.</param>
/// <param name="ProbeTitles">Its probes in order, so the choice says what it is.</param>
public sealed record ProbeEditionDto(string? Id, string? Title, bool IsTrial, IReadOnlyList<string> ProbeTitles);
