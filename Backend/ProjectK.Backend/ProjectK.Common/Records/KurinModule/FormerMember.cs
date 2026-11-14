namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// Someone who used to belong to a kurin, as that kurin is allowed to remember them.
/// <para>
/// Deliberately thin. A former member is not in the роздача of the kurin any more, so their address,
/// school, contacts and everything they have done since are none of its business — what it keeps is
/// that this person was here, in this гурток, between these dates, and enough of a name to recognise
/// them by when deciding to take them back.
/// </para>
/// </summary>
public sealed record FormerMember(
    Guid MemberKey,
    string FirstName,
    string? MiddleName,
    string LastName,
    Guid? GroupKey,
    string? GroupName,
    DateTime JoinedAtUtc,
    DateTime LeftAtUtc);
