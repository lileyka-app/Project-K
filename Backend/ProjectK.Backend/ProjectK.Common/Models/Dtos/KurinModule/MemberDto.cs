using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>
/// A person as a card or a roster shows them. The members every read fills are <c>required</c>;
/// the rest are filled afterwards by whoever knows them — the placement, the mask, the tags.
/// </summary>
public sealed record MemberDto : IHasParentContacts, IMemberPersonalData, IHasCardExtras
{
    public required Guid MemberKey { get; set; }

    /// <summary>
    /// The code a person hands to another kurin's провід so it can take them in. Only ever filled
    /// in for the person themselves — <c>MemberMapping.MaskFor</c> clears it for everyone else.
    /// </summary>
    public string? PublicId { get; set; }
    public Guid GroupKey { get; set; }

    /// <summary>
    /// The гурток's name, when the read knew it. Null from reads that answer about one person
    /// rather than a placement — the card already shows the гурток from the membership block.
    /// </summary>
    public string? GroupName { get; set; }
    public Guid KurinKey { get; set; }

    /// <summary>
    /// Whether they are в кадрі виховників of the kurin this read was about. Filled by list
    /// reads; a read about one person leaves it false, because standing is a property of a
    /// placement and there is no placement in view.
    /// </summary>
    public bool IsStaff { get; set; }

    /// <summary>
    /// The гуртки they run as виховник in that kurin, by name. Empty for a юнак. Filled by list
    /// reads and by the read of one person, for the kurin that person is seen in.
    /// </summary>
    public ICollection<string> MentoredGroupNames { get; set; } = [];

    /// <summary>The same гуртки with their keys, so the card can open them. Filled only by the read of one person.</summary>
    public ICollection<GroupRef> MentoredGroups { get; set; } = [];
    public required Guid? UserKey { get; set; }

    /// <summary>
    /// Where their account stands — whether the invitation is still waiting to be accepted. Filled
    /// only by the read of one person, and only for whoever may edit them.
    /// </summary>
    public OnboardingStatus? AccountStatus { get; set; }

    /// <summary>
    /// The kurin's tags on this person that the viewer may see. Empty when the kurin has tags off.
    /// The tags themselves come from <c>GET api/kurin/{kurinKey}/tags</c>.
    /// </summary>
    public ICollection<Guid> TagKeys { get; set; } = [];

    /// <summary>
    /// Whether the invitation letter this write was due to send went out. Null when the write sent
    /// none; false means the account stands but the letter has to be sent again.
    /// </summary>
    public bool? InvitationSent { get; set; }
    public string? UserRole { get; set; }
    public required string FirstName { get; set; }
    public required string? MiddleName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public required DateOnly? DateOfBirth { get; set; }

    /// <inheritdoc />
    public string? Birthday { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<string> HiddenData { get; set; } = [];
    public required string? Address { get; set; }
    public required string? School { get; set; }
    public string? MotherFirstName { get; set; }
    public string? MotherLastName { get; set; }
    public string? MotherPhone { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherPhone { get; set; }

    /// <summary>Values of the край's own card fields, by key; empty when the viewer may not see them.</summary>
    public IDictionary<string, string> CardExtras { get; set; } = new Dictionary<string, string>();
    public required PlastLevel? LatestPlastLevel { get; set; }
    public required ICollection<PlastLevelHistoryDto> PlastLevelHistories { get; set; }
    public required ICollection<LeadershipHistoryDto> LeadershipHistories { get; set; }
    public required ICollection<MemberWarningDto> Warnings { get; set; }
    public ICollection<MemberAwardDto> Awards { get; set; } = [];
    public required string? ProfilePhotoUrl { get; set; }
    public required MemberProfileVerificationStatus ProfileVerificationStatus { get; set; }
    public required DateTime? ProfileVerifiedAtUtc { get; set; }
    public required Guid? ProfileVerifiedByUserKey { get; set; }
    public required string? ProfileVerificationNote { get; set; }
}
