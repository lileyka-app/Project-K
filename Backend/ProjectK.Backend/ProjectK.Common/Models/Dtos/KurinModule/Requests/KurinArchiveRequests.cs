using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>A former holding corrected: its role within the same провід, and its dates.</summary>
public sealed record CorrectArchivedOfficeRequest(LeadershipRole Role, DateOnly StartDate, DateOnly EndDate);

/// <summary>A former membership corrected: its kind and its dates.</summary>
public sealed record CorrectArchivedMembershipRequest(MembershipKind Kind, DateOnly JoinedOn, DateOnly LeftOn);

/// <summary>The account proving itself again: a password, or an authenticator or recovery code.</summary>
public sealed record EnterArchiveDangerZoneRequest(string? Password, string? Code);
