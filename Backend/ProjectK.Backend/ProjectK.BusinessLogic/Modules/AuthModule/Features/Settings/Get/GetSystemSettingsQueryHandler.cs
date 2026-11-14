using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Settings.Get;

public class GetSystemSettingsQueryHandler : IRequestHandler<GetSystemSettingsQuery, ServiceResult<Dictionary<string, string>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSystemSettingsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<Dictionary<string, string>>> Handle(GetSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        var settingsList = await _unitOfWork.SystemSettings.GetAllAsync(cancellationToken);
        var settings = settingsList.ToDictionary(s => s.Key, s => s.Value);
        return new ServiceResult<Dictionary<string, string>>(ResultType.Success, settings);
    }
}
