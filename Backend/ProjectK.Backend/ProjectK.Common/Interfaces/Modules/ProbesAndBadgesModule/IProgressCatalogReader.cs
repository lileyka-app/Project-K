using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

/// <summary>
/// The catalogue's shape for whoever must write progress from outside the module. The demo seeder
/// lives in Infrastructure, which may not reference the catalogue package or the module that wraps
/// it, yet every signature and submission it writes needs a real probe, point or вмілість id.
/// Identifiers and titles only: nothing else about the catalogue is anyone's business out there.
/// </summary>
public interface IProgressCatalogReader
{
    /// <summary>Every probe with its point ids, in catalogue order.</summary>
    IReadOnlyList<CatalogProbe> GetProbes();

    /// <summary>Every вмілість id, in catalogue order.</summary>
    IReadOnlyList<string> GetBadgeIds();

    /// <summary>The вмілість's title, as the journal labels it; the id when the catalogue does not know it.</summary>
    string GetBadgeTitle(string badgeId);
}
