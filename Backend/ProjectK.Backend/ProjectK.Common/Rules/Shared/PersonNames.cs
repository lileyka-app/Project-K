namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// How a person's name is put together for people to read. The shapes differ on purpose and each
/// screen keeps its own: lists and pickers show <see cref="FirstLast"/>, documents that carry the
/// по батькові show <see cref="Full"/>, and registers sorted by surname show <see cref="SurnameFirst"/>.
/// </summary>
public static class PersonNames
{
    /// <summary>
    /// "First Last" with the outer whitespace trimmed, so a missing part leaves no stray space.
    /// Inner whitespace of the parts stays as stored; empty when both parts are blank.
    /// </summary>
    public static string FirstLast(string? firstName, string? lastName)
        => $"{firstName} {lastName}".Trim();

    /// <summary>
    /// First name, по батькові and surname joined by single spaces; blank parts are left out and the
    /// others are not trimmed.
    /// </summary>
    public static string Full(string? firstName, string? middleName, string? lastName)
        => JoinPresent(firstName, middleName, lastName);

    /// <summary>
    /// Surname, first name and по батькові joined by single spaces, as a register sorted by surname
    /// shows them; blank parts are left out and the others are not trimmed.
    /// </summary>
    public static string SurnameFirst(string? lastName, string? firstName, string? middleName = null)
        => JoinPresent(lastName, firstName, middleName);

    private static string JoinPresent(params string?[] parts)
        => string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
