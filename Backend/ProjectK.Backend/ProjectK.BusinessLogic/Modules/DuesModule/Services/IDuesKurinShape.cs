namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// What the dues module needs to know about a kurin's shape: whether it keeps its people in гуртки
/// or each in the kurin's own box, and which tags they carry — the tags set a senior kurin's rates.
/// </summary>
public interface IDuesKurinShape
{
    /// <summary>A senior kurin (УСП, УПС) has no гуртки: every person has an account in the kurin's box.</summary>
    Task<bool> IsSeniorAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>Whether the kurin uses tags now. With tags off, everyone pays the rate for everyone.</summary>
    Task<bool> TagsOnAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kurin's tags on each of its people now, by member. Someone without tags is absent; with the
    /// kurin's tags off, everyone is.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> TagsByMemberAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
