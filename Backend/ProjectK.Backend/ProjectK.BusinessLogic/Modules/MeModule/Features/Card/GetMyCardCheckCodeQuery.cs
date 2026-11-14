using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>A QR for checking the card that works for <see cref="MemberCards.CheckLifetime"/>.</summary>
public sealed record GetMyCardCheckCodeQuery : IRequest<ServiceResult<CardCodeDto>>;

public sealed class GetMyCardCheckCodeQueryHandler : IRequestHandler<GetMyCardCheckCodeQuery, ServiceResult<CardCodeDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly ICardTokenSigner _signer;
    private readonly IQrCodeService _qrCodes;
    private readonly TimeProvider _time;

    public GetMyCardCheckCodeQueryHandler(MePerson me, MemberCards cards, ICardTokenSigner signer, IQrCodeService qrCodes, TimeProvider time)
    {
        _me = me;
        _cards = cards;
        _signer = signer;
        _qrCodes = qrCodes;
        _time = time;
    }

    public async Task<ServiceResult<CardCodeDto>> Handle(GetMyCardCheckCodeQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return MeErrors.NoMember<CardCodeDto>();
        }

        if (!(await _cards.AllowanceAsync(me.Person.MemberKey, cancellationToken)).MayShare)
        {
            return ServiceResult<CardCodeDto>.Failure(
                ResultType.Forbidden, MeErrors.CardSharingNotAllowed, "This card may not be shared.");
        }

        var expires = _time.GetUtcNow().UtcDateTime + MemberCards.CheckLifetime;
        var url = _cards.SiteUrl(MemberCards.CheckPath + _signer.Sign(me.Person.MemberKey, expires));
        return new ServiceResult<CardCodeDto>(ResultType.Success, new CardCodeDto(url, _qrCodes.ToPngDataUrl(url), expires));
    }
}
