using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.Common.Models.Dtos.KraiModule.Requests;

/// <summary>A new revision: <see cref="BaseRevision"/> is the one the editor started from.</summary>
public sealed record SaveKraiProfileRequest(int BaseRevision, KraiProfileDocument Document, string? Note);
