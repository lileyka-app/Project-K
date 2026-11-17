using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Card;

/// <summary>
/// The card behind a «перевірити» QR, read live and stamped with the moment it was checked. Any
/// failure — a forged or expired token, a person who may no longer share — is the same not-found.
/// </summary>
public sealed record ReadCheckedCardQuery(string Token) : IRequest<ServiceResult<PublicCardDto>>;

public sealed class ReadCheckedCardQueryHandler : IRequestHandler<ReadCheckedCardQuery, ServiceResult<PublicCardDto>>
{
    private readonly MemberCardService _cards;
    private readonly ICardTokenSigner _signer;
    private readonly TimeProvider _time;

    public ReadCheckedCardQueryHandler(MemberCardService cards, ICardTokenSigner signer, TimeProvider time)
    {
        _cards = cards;
        _signer = signer;
        _time = time;
    }

    public async Task<ServiceResult<PublicCardDto>> Handle(ReadCheckedCardQuery request, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (_signer.Read(request.Token, now) is not { } memberKey)
        {
            return MemberErrors.NoCard<PublicCardDto>();
        }

        var allowance = await _cards.AllowanceAsync(memberKey, cancellationToken);
        return allowance.MayShare
            ? await _cards.SharedAsync(memberKey, allowance, now, cancellationToken)
            : MemberErrors.NoCard<PublicCardDto>();
    }
}
