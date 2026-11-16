namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Stops guessing one account's password, from however many addresses. The address limiter can't:
/// it has to leave room for a kurin signing in together from one Wi-Fi, and a botnet has an address per guess.
/// </summary>
public interface ILoginThrottle
{
    bool IsPaused(string email);
    void RecordFailure(string email);
    void Reset(string email);
}
