namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A kurin's requests: those it has to answer and those it sent.</summary>
public sealed record MembershipRequestsDto(
    IReadOnlyList<MembershipRequestDto> Incoming,
    IReadOnlyList<MembershipRequestDto> Outgoing);
