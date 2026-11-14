using ProjectK.Common.Records.MemberModule;


namespace ProjectK.Common.Rules.MemberModule;

/// <summary>
/// People read from the member directory, keyed for labelling rows. A person who comes back twice
/// (a current and a left membership, two kurins) is named once, by the first of them read.
/// </summary>
public static class MemberSummaryNames
{
    /// <summary>Each person's <see cref="MemberSummary.FullName"/> by member key.</summary>
    public static IReadOnlyDictionary<Guid, string> ByMember(IEnumerable<MemberSummary> people)
        => people.GroupBy(person => person.MemberKey).ToDictionary(found => found.Key, found => found.First().FullName);

    /// <summary>
    /// Each person's <see cref="MemberSummary.FullName"/> by the account they sign in with; people
    /// without an account are left out.
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> ByAccount(IEnumerable<MemberSummary> people)
        => people
            .Where(person => person.UserKey.HasValue)
            .GroupBy(person => person.UserKey!.Value)
            .ToDictionary(found => found.Key, found => found.First().FullName);
}
