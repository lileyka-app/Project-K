using System.Text.Json.Serialization;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.CardSharing;

/// <summary>What the kurin lets its people under 18 share. A kurin that never set it lets them share nothing.</summary>
public sealed record KurinCardSharingDto(
    bool MinorsMayShare,
    [property: JsonConverter(typeof(JsonNumberEnumConverter<CardField>))] CardField MinorFields,
    bool MinorsMayLink)
{
    public static KurinCardSharingDto Of(KurinCardSharing? settings) =>
        settings is null
            ? new KurinCardSharingDto(false, MemberCardShare.DefaultFields, false)
            : new KurinCardSharingDto(settings.MinorsMayShare, settings.MinorFields, settings.MinorsMayLink);
}

public sealed record GetKurinCardSharingQuery(Guid KurinKey) : IRequest<ServiceResult<KurinCardSharingDto>>;

/// <summary>
/// Sets what the kurin lets its people under 18 share. Takes effect on the next scan of any card:
/// nothing is stored on the people themselves.
/// </summary>
public sealed record SetKurinCardSharingCommand(Guid KurinKey, bool MinorsMayShare, CardField MinorFields, bool MinorsMayLink)
    : IRequest<ServiceResult<KurinCardSharingDto>>;

public sealed class GetKurinCardSharingQueryHandler : IRequestHandler<GetKurinCardSharingQuery, ServiceResult<KurinCardSharingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetKurinCardSharingQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<KurinCardSharingDto>> Handle(GetKurinCardSharingQuery request, CancellationToken cancellationToken)
    {
        var settings = await _unitOfWork.KurinCardSharing.GetForKurinAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<KurinCardSharingDto>(ResultType.Success, KurinCardSharingDto.Of(settings));
    }
}

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
            return new ServiceResult<KurinCardSharingDto>(ResultType.Forbidden);
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

        return new ServiceResult<KurinCardSharingDto>(ResultType.Success, KurinCardSharingDto.Of(settings));
    }
}
