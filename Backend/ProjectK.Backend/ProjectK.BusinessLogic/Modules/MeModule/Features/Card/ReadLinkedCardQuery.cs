using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>The card behind a permanent link; not-found once the link is off or no longer allowed.</summary>
public sealed record ReadLinkedCardQuery(string Token) : IRequest<ServiceResult<PublicCardDto>>;

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
            return MeErrors.NoCard<PublicCardDto>();
        }

        var allowance = await _cards.AllowanceAsync(share.MemberKey, cancellationToken);
        return allowance.MayLink
            ? await _cards.SharedAsync(share.MemberKey, allowance, null, cancellationToken)
            : MeErrors.NoCard<PublicCardDto>();
    }
}
