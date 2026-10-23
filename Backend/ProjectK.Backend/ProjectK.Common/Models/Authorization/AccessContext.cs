namespace ProjectK.Common.Models.Authorization;

/// <summary>
/// What an account may do, and where. Resolved fresh for the kurin the account is currently in:
/// a виховник of one kurin is a виховник there and an ordinary member everywhere else, which is a
/// sentence that cannot be said by roles stored on the account itself.
/// <para>
/// It is the one answer to "what roles does this person have". Nothing reads the identity store for
/// that any more — it holds <see cref="SystemRole.Admin"/> and nothing else that matters here.
/// </para>
/// </summary>
public sealed record AccessContext(
    Guid UserKey,
    Guid? KurinKey,
    IReadOnlyCollection<string> Roles)
{
    public bool IsAdmin => Roles.Contains(SystemRole.Admin, StringComparer.OrdinalIgnoreCase);

    /// <summary>An account with no kurin and nothing but the baseline. Used where there is no user.</summary>
    public static AccessContext None(Guid userKey) => new(userKey, null, [SystemRole.Member]);
}
