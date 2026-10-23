namespace ProjectK.Common.Entities.AuthModule;

/// <summary>
/// A duty the person took off their own «Справи». It stays off while the duty is the same as when it
/// was hidden — <see cref="Fingerprint"/> unchanged, or a count that has not grown — and comes back
/// when more lands in it. Only theirs: nobody else's list changes.
/// <para>The cleanup service deletes a row thirty days after <see cref="DismissedAtUtc"/>, so the duty comes back then too.</para>
/// </summary>
public class DismissedDuty
{
    public const int DutyKeyMaxLength = 200;
    public const int FingerprintMaxLength = 64;

    public Guid DismissedDutyKey { get; set; } = Guid.NewGuid();
    public Guid UserKey { get; set; }

    /// <summary>Which duty: its kind, kurin, гурток, event and occurrence, as the duties read writes it.</summary>
    public string DutyKey { get; set; } = string.Empty;

    /// <summary>How big the duty was when hidden — the count for one that counts things.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public DateTime DismissedAtUtc { get; set; }

    public AppUser? User { get; set; }
}
