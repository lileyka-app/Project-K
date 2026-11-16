namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// The kurin core's write door for a roster import: open the гуртки a file names that the kurin
/// does not have yet.
/// <para>
/// A write contract on purpose. An event does not fit: the import places people in these гуртки in
/// the same transaction, so it needs their keys back at once, and a failed file must take the new
/// гуртки back out with everything else.
/// </para>
/// </summary>
public interface IKurinGroupWriter
{
    /// <summary>Opens one гурток per name in the kurin and saves them. The new keys, by name.</summary>
    Task<IReadOnlyDictionary<string, Guid>> OpenAsync(Guid kurinKey, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default);
}
