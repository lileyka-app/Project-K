namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// A закріплення за гуртком of a person on a roster, in the roster's kurin, revoked ones included:
/// whether it still counts is the reader's question, not the read's.
/// </summary>
public sealed record MemberListAssignment(string GroupName, Guid GroupKurinKey, DateTime? RevokedAtUtc);
