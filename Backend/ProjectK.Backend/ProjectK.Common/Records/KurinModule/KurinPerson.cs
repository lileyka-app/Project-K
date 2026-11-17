using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// A person as a kurin holds them: who they are, and where in that kurin they stand now. The
/// placement is one open membership, so the same person read from another kurin answers differently.
/// </summary>
public sealed record KurinPerson(MemberSummary Person, Guid KurinKey, Guid? GroupKey)
{
    public Guid MemberKey => Person.MemberKey;

    public Guid? UserKey => Person.UserKey;
}
