using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>
/// The card behind a «перевірити» QR, read live and stamped with the moment it was checked. Any
/// failure — a forged or expired token, a person who may no longer share — is the same not-found.
/// </summary>
public sealed record ReadCheckedCardQuery(string Token) : IRequest<ServiceResult<PublicCardDto>>;

/// <summary>The card behind a permanent link; not-found once the link is off or no longer allowed.</summary>
public sealed record ReadLinkedCardQuery(string Token) : IRequest<ServiceResult<PublicCardDto>>;

public sealed class ReadCheckedCardQueryHandler : IRequestHandler<ReadCheckedCardQuery, ServiceResult<PublicCardDto>>
{
    private readonly MemberCards _cards;
    private readonly ICardTokenSigner _signer;
    private readonly TimeProvider _time;

    public ReadCheckedCardQueryHandler(MemberCards cards, ICardTokenSigner signer, TimeProvider time)
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
            return new ServiceResult<PublicCardDto>(ResultType.NotFound);
        }

        var allowance = await _cards.AllowanceAsync(memberKey, cancellationToken);
        return allowance.MayShare
            ? await _cards.SharedAsync(memberKey, allowance, now, cancellationToken)
            : new ServiceResult<PublicCardDto>(ResultType.NotFound);
    }
}

public sealed class ReadLinkedCardQueryHandler : IRequestHandler<ReadLinkedCardQuery, ServiceResult<PublicCardDto>>
{
    private readonly MemberCards _cards;
    private readonly IUnitOfWork _unitOfWork;

    public ReadLinkedCardQueryHandler(MemberCards cards, IUnitOfWork unitOfWork)
    {
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<PublicCardDto>> Handle(ReadLinkedCardQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 64
            || await _unitOfWork.MemberCardShares.GetByLinkTokenAsync(request.Token, cancellationToken) is not { } share)
        {
            return new ServiceResult<PublicCardDto>(ResultType.NotFound);
        }

        var allowance = await _cards.AllowanceAsync(share.MemberKey, cancellationToken);
        return allowance.MayLink
            ? await _cards.SharedAsync(share.MemberKey, allowance, null, cancellationToken)
            : new ServiceResult<PublicCardDto>(ResultType.NotFound);
    }
}
