namespace ProjectK.Common.Interfaces.Modules.KraiModule;

/// <summary>
/// The name a вмілість shows for its край. The catalogue knows only codes, so a край an admin added
/// or renamed is told here, or its вмілості keep showing the code or the name it was seeded with.
/// </summary>
public interface ICatalogKraiNames
{
    void Name(string kraiCode, string name);
}
