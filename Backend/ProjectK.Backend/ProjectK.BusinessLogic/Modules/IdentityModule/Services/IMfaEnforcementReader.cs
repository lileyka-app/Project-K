namespace ProjectK.BusinessLogic.Modules.IdentityModule.Services;

public interface IMfaEnforcementReader
{
    Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken = default);
}
