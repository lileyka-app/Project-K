using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;

/// <summary>Forgets one of the caller's devices. Idempotent: an unknown endpoint is already forgotten.</summary>
public sealed record UnsubscribeFromPushCommand(string Endpoint) : IRequest<ServiceResult<bool>>;

public sealed class UnsubscribeFromPushCommandHandler
    : IRequestHandler<UnsubscribeFromPushCommand, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public UnsubscribeFromPushCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<bool>> Handle(UnsubscribeFromPushCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return ServiceResult<bool>.Failure(ResultType.Unauthorized, InfrastructureErrors.Unauthorized, "No one is signed in.");
        }

        var device = await _unitOfWork.WebPushSubscriptions.GetByEndpointAsync(request.Endpoint, cancellationToken);
        // Somebody else's device is not the caller's to forget; the answer is the same either way.
        if (device is null || device.UserKey != _currentUserContext.UserId.Value)
        {
            return new ServiceResult<bool>(ResultType.Success, false);
        }

        _unitOfWork.WebPushSubscriptions.Delete(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
