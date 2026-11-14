using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.GetWaitlistEntries;

public class GetWaitlistEntriesQueryHandler : IRequestHandler<GetWaitlistEntriesQuery, ServiceResult<IEnumerable<WaitlistEntry>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetWaitlistEntriesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IEnumerable<WaitlistEntry>>> Handle(GetWaitlistEntriesQuery request, CancellationToken cancellationToken)
    {
        var entries = await _unitOfWork.WaitlistEntries.GetAllAsync(cancellationToken);
        return new ServiceResult<IEnumerable<WaitlistEntry>>(ResultType.Success, entries);
    }
}
