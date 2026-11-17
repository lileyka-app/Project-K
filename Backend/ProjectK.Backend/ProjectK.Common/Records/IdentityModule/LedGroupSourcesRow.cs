namespace ProjectK.Common.Records.IdentityModule;

/// <summary>
/// What an account holds over гуртки in one kurin, each source as stored: the гуртки of the гуртковий
/// offices it sits in there, those of its mentor assignments in force (not narrowed to the kurin), and
/// the гурток it is a member of. Which of them make the "own groups" tier is <c>LedGroupRules</c>' to say.
/// </summary>
public sealed record LedGroupSourcesRow(
    IReadOnlyList<Guid> OfficeGroupKeys,
    IReadOnlyList<Guid> MentorGroupKeys,
    Guid? OwnGroupKey);
