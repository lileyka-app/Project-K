using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Dtos.ArchiveModule.Requests;

/// <summary>A former membership corrected: its kind and its dates.</summary>
public sealed record CorrectArchivedMembershipRequest(MembershipKind Kind, DateOnly JoinedOn, DateOnly LeftOn);
