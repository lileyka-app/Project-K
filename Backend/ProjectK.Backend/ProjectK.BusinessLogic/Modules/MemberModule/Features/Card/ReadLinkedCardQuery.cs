using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Card;

/// <summary>
/// The card behind a permanent link; not-found once the link is off or no longer allowed, and a link
/// no longer allowed is dropped then and there.
/// </summary>
public sealed record ReadLinkedCardQuery(string Token) : IRequest<ServiceResult<PublicCardDto>>;

public sealed class ReadLinkedCardQueryHandler : IRequestHandler<ReadLinkedCardQuery, ServiceResult<PublicCardDto>>
{
    private readonly MemberCardService _cards;
    private readonly IUnitOfWork _unitOfWork;

    public ReadLinkedCardQueryHandler(MemberCardService cards, IUnitOfWork unitOfWork)
    {
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<PublicCardDto>> Handle(ReadLinkedCardQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 64
            || await _unitOfWork.MemberCardShares.GetByLinkTokenAsync(request.Token, cancellationToken) is not { } share)
        {
            return MemberErrors.NoCard<PublicCardDto>();
        }

        var allowance = await _cards.AllowanceAsync(share.MemberKey, cancellationToken);
        if (!allowance.MayLink)
        {
            await _cards.ForgetLinkAsync(share, cancellationToken);
            return MemberErrors.NoCard<PublicCardDto>();
        }

        return await _cards.SharedAsync(share.MemberKey, allowance, null, cancellationToken);
    }
}
