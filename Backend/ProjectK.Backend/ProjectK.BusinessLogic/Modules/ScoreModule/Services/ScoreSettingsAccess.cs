using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

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

        return await _access.MayManageAsync(kurinKey, cancellationToken) ? null : ScoreAccess.Forbidden<T>();
    }
}
