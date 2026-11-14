using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Settings;

public record UpdateSystemSettingCommand(string Key, string Value) : IRequest<ServiceResult<Unit>>;

public class UpdateSystemSettingCommandHandler : IRequestHandler<UpdateSystemSettingCommand, ServiceResult<Unit>>
{
    private readonly ISystemSettingsService _systemSettings;

    public UpdateSystemSettingCommandHandler(ISystemSettingsService systemSettings)
    {
        _systemSettings = systemSettings;
    }

    public async Task<ServiceResult<Unit>> Handle(UpdateSystemSettingCommand request, CancellationToken cancellationToken)
    {
        await _systemSettings.SetValueAsync(request.Key, request.Value, cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
