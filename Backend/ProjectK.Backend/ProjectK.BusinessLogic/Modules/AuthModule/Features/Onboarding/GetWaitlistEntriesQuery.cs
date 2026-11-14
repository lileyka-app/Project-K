using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;

public record GetWaitlistEntriesQuery : IRequest<ServiceResult<IEnumerable<WaitlistEntry>>>;

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
