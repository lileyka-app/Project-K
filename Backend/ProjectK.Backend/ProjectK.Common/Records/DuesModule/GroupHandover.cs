namespace ProjectK.Common.Records.DuesModule;

/// <summary>How one гурток stands with the kurin: what it owes up, what it handed over, what arrived.</summary>
public sealed record GroupHandover(
    Guid GroupKey,
    decimal OwedUp,
    decimal Transferred,
    decimal Received)
{
    /// <summary>Collected for the станиця and the kurin but not handed over yet.</summary>
    public decimal Outstanding => OwedUp - Transferred;
}
