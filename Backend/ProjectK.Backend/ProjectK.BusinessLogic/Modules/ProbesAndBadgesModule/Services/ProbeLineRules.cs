using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// The probes one person walks, one after another, when the catalogue holds several editions of
/// the programme side by side. A person walks the edition their гурток chose — unless they have
/// already closed a probe past the first place (знак прихильника is place 0) in some edition: then
/// they finish that one, so an old first probe is never followed by a new second whose points were
/// moved about. A place their edition lacks still shows whatever they started there in another one.
/// Clients get the line from <c>api/member/{memberKey}/probes/line</c> rather than working it out.
/// </summary>
public static class ProbeLineRules
{
    /// <param name="progress">The person's probes that have a progress row, by id.</param>
    /// <param name="groupEdition">The edition their гурток walks; null for the unnamed one.</param>
    public static IReadOnlyList<ProbeSummaryDto> Of(
        IReadOnlyList<ProbeSummaryDto> catalog,
        IReadOnlyDictionary<string, ProbeProgressStatus> progress,
        string? groupEdition)
    {
        var edition = HeldBy(catalog, progress) is { } held
            ? held.Edition
            : catalog.Any(probe => probe.Edition == groupEdition) ? groupEdition : null;

        var own = catalog.Where(probe => probe.Edition == edition).ToList();
        var places = own.Select(probe => probe.Order).ToHashSet();
        var elsewhere = catalog
            .Where(probe => probe.Edition != edition && progress.ContainsKey(probe.Id) && !places.Contains(probe.Order))
            .GroupBy(probe => probe.Order)
            .Select(place => place.First());

        return [.. own.Concat(elsewhere).OrderBy(probe => probe.Order)];
    }

    /// <summary>
    /// The closed probe that holds a person to its edition: the furthest one past the first place.
    /// Null when nothing holds them and their гурток decides.
    /// </summary>
    public static ProbeSummaryDto? HeldBy(
        IReadOnlyList<ProbeSummaryDto> catalog,
        IReadOnlyDictionary<string, ProbeProgressStatus> progress)
    {
        return catalog
            .Where(probe => probe.Order >= 1 && progress.TryGetValue(probe.Id, out var status) && IsClosed(status))
            .OrderByDescending(probe => probe.Order)
            .FirstOrDefault();
    }

    public static bool IsClosed(ProbeProgressStatus status) =>
        status is ProbeProgressStatus.Completed or ProbeProgressStatus.Verified;
}
