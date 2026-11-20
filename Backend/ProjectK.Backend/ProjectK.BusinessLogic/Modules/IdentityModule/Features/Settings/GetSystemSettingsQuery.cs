using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Settings;

public sealed record GetSystemSettingsQuery() : IRequest<ServiceResult<Dictionary<string, string>>>;

public sealed class GetSystemSettingsQueryHandler : IRequestHandler<GetSystemSettingsQuery, ServiceResult<Dictionary<string, string>>>
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
