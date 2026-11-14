using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;

/// <summary>Brings hidden duties back to the person's «Справи»: the one under <see cref="Key"/>, or every one when it is null.</summary>
public sealed record RestoreDutiesCommand(string? Key) : IRequest<ServiceResult<Unit>>;

public sealed class RestoreDutiesCommandHandler : IRequestHandler<RestoreDutiesCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public RestoreDutiesCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<Unit>> Handle(RestoreDutiesCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey || userKey == Guid.Empty)
        {
            return MeErrors.SignInRequired<Unit>();
        }

        var key = string.IsNullOrWhiteSpace(request.Key) ? null : request.Key;
        foreach (var dismissal in await _unitOfWork.DismissedDuties.GetToChangeAsync(userKey, key, cancellationToken))
        {
            _unitOfWork.DismissedDuties.Delete(dismissal, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
