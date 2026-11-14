using System.Text.Json.Serialization;
using MediatR;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>
/// Sets what the kurin lets its people under 18 share. Takes effect on the next scan of any card:
/// nothing is stored on the people themselves.
/// </summary>
public sealed record SetKurinCardSharingCommand(Guid KurinKey, bool MinorsMayShare, CardField MinorFields, bool MinorsMayLink)
    : IRequest<ServiceResult<KurinCardSharingDto>>;

public sealed class SetKurinCardSharingCommandHandler : IRequestHandler<SetKurinCardSharingCommand, ServiceResult<KurinCardSharingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public SetKurinCardSharingCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<KurinCardSharingDto>> Handle(SetKurinCardSharingCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return ServiceResult<KurinCardSharingDto>.Failure(ResultType.Forbidden, KurinErrors.CardSharingOtherKurin, "Card sharing is set from inside the kurin.");
        }

        var settings = await _unitOfWork.KurinCardSharing.GetForKurinAsync(request.KurinKey, cancellationToken);
        if (settings is null)
        {
            settings = new KurinCardSharing { KurinKey = request.KurinKey };
            _unitOfWork.KurinCardSharing.Create(settings);
        }

        settings.MinorsMayShare = request.MinorsMayShare;
        settings.MinorFields = request.MinorFields & CardField.All;
        settings.MinorsMayLink = request.MinorsMayShare && request.MinorsMayLink;
        settings.SetByUserKey = _currentUser.UserId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<KurinCardSharingDto>(ResultType.Success, settings.ToDto());
    }
}
