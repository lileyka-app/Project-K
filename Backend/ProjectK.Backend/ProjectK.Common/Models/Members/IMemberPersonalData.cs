namespace ProjectK.Common.Models.Members;

/// <summary>
/// A shape that carries a person's personal data out of the API, so <see cref="MemberFieldMask"/>
/// can blank what the viewer may not see. A shape that also carries the parents' contacts says so
/// with <see cref="IHasParentContacts"/>, and one that carries the край's own fields with
/// <see cref="IHasCardExtras"/>.
/// </summary>
public interface IMemberPersonalData
{
    string Email { get; set; }

    string PhoneNumber { get; set; }

    DateOnly? DateOfBirth { get; set; }

    /// <summary>The day and month of birth as <c>MM-dd</c>, without the year. Filled by the mask.</summary>
    string? Birthday { get; set; }

    string? Address { get; set; }

    string? School { get; set; }

    /// <summary>
    /// The groups of <see cref="MemberDataFields"/> blanked for this viewer, by name, so a client can
    /// say «Приховано» instead of «Не вказано». Filled by the mask.
    /// </summary>
    IReadOnlyList<string> HiddenData { get; set; }
}

/// <summary>A shape that carries the values of the край's own card fields.</summary>
public interface IHasCardExtras
{
    IDictionary<string, string> CardExtras { get; set; }
}
