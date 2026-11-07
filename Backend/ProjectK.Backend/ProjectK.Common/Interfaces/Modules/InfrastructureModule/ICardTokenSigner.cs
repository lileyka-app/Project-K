namespace ProjectK.Common.Interfaces.Modules.InfrastructureModule;

/// <summary>
/// Short-lived tokens behind the «перевірити картку» QR: a member key and an expiry, signed so that
/// nobody can make one, and short enough for a QR a phone camera reads at a glance. Nothing is
/// stored; a token stops working when it expires.
/// </summary>
public interface ICardTokenSigner
{
    string Sign(Guid memberKey, DateTime expiresAtUtc);

    /// <summary>The member a token names, or null when it is forged, broken or expired at <paramref name="nowUtc"/>.</summary>
    Guid? Read(string token, DateTime nowUtc);
}
