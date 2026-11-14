using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>
/// The transfer code as a QR: a link that opens «Склад куреня» with the code already typed in, for
/// the Звʼязковий of the kurin the person is going to.
/// </summary>
public sealed record GetMyTransferCodeQuery : IRequest<ServiceResult<CardCodeDto>>;

public sealed class GetMyTransferCodeQueryHandler : IRequestHandler<GetMyTransferCodeQuery, ServiceResult<CardCodeDto>>
{
    public const string TransferPath = "/kurin/transfers?code=";

    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly IMemberDirectory _members;
    private readonly IQrCodeService _qrCodes;

    public GetMyTransferCodeQueryHandler(MePerson me, MemberCards cards, IMemberDirectory members, IQrCodeService qrCodes)
    {
        _me = me;
        _cards = cards;
        _members = members;
        _qrCodes = qrCodes;
    }

    public async Task<ServiceResult<CardCodeDto>> Handle(GetMyTransferCodeQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return MeErrors.NoMember<CardCodeDto>();
        }

        if (await _members.GetPublicIdAsync(me.Person.MemberKey, cancellationToken) is not { } publicId)
        {
            return ServiceResult<CardCodeDto>.Failure(ResultType.NotFound, MeErrors.PublicIdNotFound, "The member record has no public id.");
        }

        var url = _cards.SiteUrl(TransferPath + publicId);
        return new ServiceResult<CardCodeDto>(ResultType.Success, new CardCodeDto(url, _qrCodes.ToPngDataUrl(url), null));
    }
}
