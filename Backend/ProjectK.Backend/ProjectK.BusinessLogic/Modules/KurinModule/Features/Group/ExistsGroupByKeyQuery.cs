using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public class ExistsGroupByKeyQuery(Guid groupKey) : IRequest<ServiceResult<bool>>
{
    public Guid GroupKey { get; set; } = groupKey;
}

public class ExistsGroupByKeyQueryHandler : IRequestHandler<ExistsGroupByKeyQuery, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    public ExistsGroupByKeyQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<ServiceResult<bool>> Handle(ExistsGroupByKeyQuery request, CancellationToken cancellationToken)
    {
        var exists = await _unitOfWork.Groups.ExistsAsync(request.GroupKey, cancellationToken);
        return new ServiceResult<bool>(Common.Models.Enums.ResultType.Success, exists);
    }
}
