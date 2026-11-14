using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>
/// A край's profile for the admin editor: the stored document, not the resolved values.
/// <see cref="Revision"/> is the one in force; 0 when none was ever saved.
/// </summary>
public sealed record KraiProfileEditorDto(string KraiCode, string KraiName, int Revision, KraiProfileDocument Document);
