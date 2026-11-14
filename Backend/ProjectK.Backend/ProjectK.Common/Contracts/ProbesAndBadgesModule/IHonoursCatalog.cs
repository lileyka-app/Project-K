using ProjectK.Common.Models.Catalog;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

/// <summary>The ступені and senior відзначення with their pictures, as last fetched.</summary>
public interface IHonoursCatalog
{
    /// <summary>Whether a catalogue has been read at all; until then nothing can be checked against it.</summary>
    bool IsLoaded { get; }

    IReadOnlyList<CatalogHonour> GetAll();

    CatalogHonour? GetById(string id);
}
