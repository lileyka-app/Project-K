namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

public interface IMfaEnforcementReader
{
    Task<bool> IsPrivilegedMfaRequiredAsync(CancellationToken cancellationToken = default);
}
