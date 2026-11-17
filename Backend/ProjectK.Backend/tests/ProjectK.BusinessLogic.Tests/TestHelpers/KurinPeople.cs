using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>Someone as a kurin holds them, for directory setups: the person and where they stand.</summary>
internal static class KurinPeople
{
    public static KurinPerson Of(
        Guid memberKey,
        Guid? userKey,
        Guid kurinKey,
        Guid? groupKey,
        string firstName,
        string lastName,
        string email,
        string? profilePhotoBlobName)
        => new(new MemberSummary(memberKey, userKey, firstName, lastName, email, profilePhotoBlobName), kurinKey, groupKey);

    /// <summary>Someone nameless standing in a kurin, for setups that only ask where.</summary>
    public static KurinPerson In(Guid memberKey, Guid kurinKey)
        => Of(memberKey, null, kurinKey, null, string.Empty, string.Empty, string.Empty, null);
}
