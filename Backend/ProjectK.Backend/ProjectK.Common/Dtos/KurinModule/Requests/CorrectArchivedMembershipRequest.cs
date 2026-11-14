using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>A former membership corrected: its kind and its dates.</summary>
public sealed record CorrectArchivedMembershipRequest(MembershipKind Kind, DateOnly JoinedOn, DateOnly LeftOn);
