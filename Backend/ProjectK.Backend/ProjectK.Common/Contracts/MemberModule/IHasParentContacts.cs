namespace ProjectK.Common.Contracts.MemberModule;

/// <summary>A member's parents' contacts: the mother's and the father's name and phone.</summary>
public interface IHasParentContacts
{
    string? MotherFirstName { get; set; }
    string? MotherLastName { get; set; }
    string? MotherPhone { get; set; }
    string? FatherFirstName { get; set; }
    string? FatherLastName { get; set; }
    string? FatherPhone { get; set; }
}
