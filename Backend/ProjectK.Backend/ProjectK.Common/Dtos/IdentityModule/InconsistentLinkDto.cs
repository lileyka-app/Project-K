namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>A member and its account that disagree on the kurin. Nothing produces it any more; the list stays in the report.</summary>
public sealed record InconsistentLinkDto(Guid MemberKey, Guid UserKey, Guid MemberKurinKey, Guid? UserKurinKey);
