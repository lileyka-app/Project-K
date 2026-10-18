using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Models;

/// <summary>A край's profile for the admin editor: the stored document, not the resolved values.</summary>
public sealed class KraiProfileEditorResponse
{
    public string KraiCode { get; init; } = string.Empty;
    public string KraiName { get; init; } = string.Empty;

    /// <summary>The revision in force; 0 when none was ever saved.</summary>
    public int Revision { get; init; }

    public KraiProfileDocument Document { get; init; } = new();
}

public sealed class KraiProfileRevisionResponse
{
    public int Revision { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? CreatedByName { get; init; }
    public string? Note { get; init; }
}

/// <summary>A new revision: <see cref="BaseRevision"/> is the one the editor started from.</summary>
public sealed class SaveKraiProfileRequest
{
    public int BaseRevision { get; init; }
    public KraiProfileDocument Document { get; init; } = new();
    public string? Note { get; init; }
}

/// <summary>
/// A край to add. <see cref="Document"/> is its first profile; left out, the край reads Ukraine's
/// вмілості and the defaults in code for the rest.
/// </summary>
public sealed class CreateKraiRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public KraiProfileDocument? Document { get; init; }
}

public sealed class RenameKraiRequest
{
    public string Name { get; init; } = string.Empty;
}
