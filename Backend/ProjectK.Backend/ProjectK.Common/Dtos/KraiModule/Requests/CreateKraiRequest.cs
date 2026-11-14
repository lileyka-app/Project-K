using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.Common.Dtos.KraiModule.Requests;

/// <summary>
/// A край to add. <see cref="Document"/> is its first profile; left out, the край reads Ukraine's
/// вмілості and the defaults in code for the rest.
/// </summary>
public sealed record CreateKraiRequest(string Code, string Name, KraiProfileDocument? Document);
