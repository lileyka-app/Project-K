using ProjectK.Common.Models.Authorization.KurinPolicy;

namespace ProjectK.Common.Models.Members;

/// <summary>
/// What one viewer may see of other people's data, worked out once from the kurin's policy: for each
/// group of <see cref="MemberDataFields"/> how far it reaches, and which гуртки count as theirs.
/// Every read that hands a person's data out passes it through <see cref="Apply"/>; every write
/// keeps the groups <see cref="HiddenFrom"/> names as they are stored.
/// <para>
/// A person always sees their own data. The birthday is part of the date of birth, so whoever sees
/// the date sees the birthday too.
/// </para>
/// </summary>
public sealed class MemberFieldMask
{
    private readonly IReadOnlyDictionary<MemberDataGroup, CapabilityReach> _reach;
    private readonly HashSet<Guid> _ownGroupKeys;

    /// <summary>A mask for one viewer.</summary>
    /// <param name="viewerUserKey">The viewer's account; their own record is never masked.</param>
    /// <param name="reach">How far each group reaches; a group left out reaches nobody.</param>
    /// <param name="ownGroupKeys">The гуртки an <see cref="CapabilityReach.OwnGroup"/> reach covers.</param>
    public MemberFieldMask(
        Guid? viewerUserKey,
        IReadOnlyDictionary<MemberDataGroup, CapabilityReach> reach,
        IEnumerable<Guid> ownGroupKeys)
    {
        ViewerUserKey = viewerUserKey;
        _reach = reach;
        _ownGroupKeys = ownGroupKeys.ToHashSet();
    }

    /// <summary>A mask that hides nothing: for readers the policy does not apply to.</summary>
    public static MemberFieldMask ShowsEverything { get; } = new(
        null,
        MemberDataFields.All.ToDictionary(info => info.Group, _ => CapabilityReach.Kurin),
        []);

    /// <summary>The viewer's account.</summary>
    public Guid? ViewerUserKey { get; }

    /// <summary>Whether the person behind <paramref name="memberUserKey"/> is the viewer.</summary>
    public bool IsSelf(Guid? memberUserKey) =>
        memberUserKey.HasValue && ViewerUserKey.HasValue && memberUserKey.Value == ViewerUserKey.Value;

    /// <summary>Whether the viewer sees <paramref name="group"/> of a person placed in <paramref name="memberGroupKey"/>.</summary>
    public bool Shows(MemberDataGroup group, Guid? memberUserKey, Guid? memberGroupKey)
    {
        if (IsSelf(memberUserKey) || Reaches(group, memberGroupKey))
        {
            return true;
        }

        return group == MemberDataGroup.Birthday && Reaches(MemberDataGroup.BirthDate, memberGroupKey);
    }

    /// <summary>
    /// The groups the viewer does not see of this person. A write by the viewer keeps these as
    /// stored: a form that showed «Приховано» cannot be allowed to wipe what it never had.
    /// </summary>
    public IReadOnlySet<MemberDataGroup> HiddenFrom(Guid? memberUserKey, Guid? memberGroupKey) =>
        MemberDataFields.All
            .Select(info => info.Group)
            .Where(group => !Shows(group, memberUserKey, memberGroupKey))
            .ToHashSet();

    /// <summary>
    /// Fills the birthday, blanks every group the viewer does not see and names those groups in
    /// <see cref="IMemberPersonalData.HiddenData"/>.
    /// </summary>
    public void Apply(IMemberPersonalData target, Guid? memberUserKey, Guid? memberGroupKey)
    {
        target.Birthday = MemberDataFields.BirthdayOf(target.DateOfBirth);

        var hidden = HiddenFrom(memberUserKey, memberGroupKey);
        foreach (var group in hidden)
        {
            MemberDataFields.Hide(target, group);
        }

        target.HiddenData = MemberDataFields.All
            .Where(info => hidden.Contains(info.Group))
            .Select(info => info.Name)
            .ToList();
    }

    private bool Reaches(MemberDataGroup group, Guid? memberGroupKey) =>
        _reach.GetValueOrDefault(group) switch
        {
            CapabilityReach.Kurin => true,
            CapabilityReach.OwnGroup => memberGroupKey.HasValue && _ownGroupKeys.Contains(memberGroupKey.Value),
            _ => false
        };
}
