namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// An account opened for a member, and whether the letter inviting them to it actually went out.
/// The account stands either way; a letter that did not go can be sent again from the member.
/// </summary>
public sealed record MemberInvitation(Guid UserKey, bool Sent);
