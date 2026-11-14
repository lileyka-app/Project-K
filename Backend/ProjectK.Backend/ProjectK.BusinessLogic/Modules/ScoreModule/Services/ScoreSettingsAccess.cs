using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>What every settings write shares: it is the caller's kurin, and they manage its score.</summary>
public sealed class ScoreSettingsAccess
{
    private readonly ScoreAccess _access;

    public ScoreSettingsAccess(ScoreAccess access)
    {
        _access = access;
    }

    public async Task<ServiceResult<T>?> RefuseAsync<T>(Guid kurinKey, CancellationToken cancellationToken)
    {
        if (_access.Refuse<T>(kurinKey) is { } refused)
        {
            return refused;
        }

        return await _access.MayManageAsync(kurinKey, cancellationToken) ? null : ScoreErrors.Refused<T>();
    }
}
