using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;

public sealed class ExistsGroupByKeyQuery(Guid groupKey) : IRequest<ServiceResult<bool>>
{
    public Guid GroupKey { get; set; } = groupKey;
}

public sealed class ExistsGroupByKeyQueryHandler : IRequestHandler<ExistsGroupByKeyQuery, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    public ExistsGroupByKeyQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    public async Task<ServiceResult<bool>> Handle(ExistsGroupByKeyQuery request, CancellationToken cancellationToken)
    {
        var exists = await _unitOfWork.Groups.ExistsAsync(request.GroupKey, cancellationToken);
        return new ServiceResult<bool>(Common.Enums.Shared.ResultType.Success, exists);
    }
}
