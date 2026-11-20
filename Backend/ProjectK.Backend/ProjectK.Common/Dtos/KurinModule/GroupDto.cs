namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A гурток as the kurin's pages see it.</summary>
/// <param name="GroupKey">The гурток.</param>
/// <param name="KurinKey">The kurin it is in.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">The гурток's own words about itself.</param>
/// <param name="SilhouetteUrl">The гурток's silhouette, as a link that expires; null when it has none.</param>
/// <param name="KurinNumber">The number of the kurin it is in; null when that kurin is підготовчий or was not read.</param>
public sealed record GroupDto(
    Guid GroupKey,
    Guid KurinKey,
    string Name,
    string? Description,
    string? SilhouetteUrl,
    int? KurinNumber);
