namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// The kurin module's write door for an account activation that founds a kurin: take the founder
/// into the kurin their approval opened, and seat them as its Зв'язковий. Each call runs the kurin
/// module's own use case, so the rules of membership and offices stay where they are. The person
/// behind the account is opened earlier, by the <c>AccountActivated</c> event; this door only places
/// the person that event wrote.
/// <para>
/// A write contract on purpose: an event does not fit. Activation answers with a signed-in session,
/// and the kurin and roles in it are read from the membership and the office seated here, so both must be
/// written before the answer is built, not by a handler reacting after the save. The office is also
/// seated by the system, because the activating caller is still anonymous and nobody could authorize
/// the assignment.
/// </para>
/// <para>
/// Neither call reports a refusal back: activation goes on and signs the person in either way.
/// </para>
/// </summary>
public interface IKurinFounderWriter
{
    /// <summary>
    /// Takes the person into the kurin they founded. Joining rather than placing: someone who
    /// already belongs to a kurin keeps it and founds the new one alongside.
    /// </summary>
    Task JoinFoundedKurinAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seats the person as the kurin's Зв'язковий from today. The office sync grants the role.
    /// </summary>
    Task SeatFounderAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default);
}
