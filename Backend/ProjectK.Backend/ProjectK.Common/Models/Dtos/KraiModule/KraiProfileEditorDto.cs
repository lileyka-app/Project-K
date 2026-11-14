using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.Common.Models.Dtos.KraiModule;

/// <summary>A край's profile for the admin editor: the stored document, not the resolved values.</summary>
/// <param name="Revision">The revision in force; 0 when none was ever saved.</param>
public sealed record KraiProfileEditorDto(string KraiCode, string KraiName, int Revision, KraiProfileDocument Document);
