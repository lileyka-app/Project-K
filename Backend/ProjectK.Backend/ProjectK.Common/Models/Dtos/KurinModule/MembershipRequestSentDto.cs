namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>
/// What sending a request did. A person who stands in no kurin has nobody to ask: they are taken in
/// at once and <see cref="Joined"/> is true.
/// </summary>
public sealed record MembershipRequestSentDto(Guid? MembershipRequestKey, bool Joined, bool PersonAnswers = false);
