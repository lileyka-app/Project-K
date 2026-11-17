using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Card;

/// <summary>Makes a permanent card link, replacing any earlier one: the old link stops working.</summary>
public sealed record IssueMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

public sealed class IssueMyCardLinkCommandHandler : IRequestHandler<IssueMyCardLinkCommand, ServiceResult<MyCardDto>>
{
    private readonly ICurrentMember _me;
    private readonly MemberCardService _cards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public IssueMyCardLinkCommandHandler(ICurrentMember me, MemberCardService cards, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(IssueMyCardLinkCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        if (me is null)
        {
            return MemberErrors.NoMember<MyCardDto>();
        }

        var memberKey = me.Person.MemberKey;
        if (!(await _cards.AllowanceAsync(memberKey, cancellationToken)).MayLink)
        {
            return ServiceResult<MyCardDto>.Failure(
                ResultType.Forbidden, MemberErrors.CardLinkNotAllowed, "A permanent link is not allowed for this card.");
        }

        var share = await _cards.ShareOfAsync(memberKey, cancellationToken);
        share.LinkToken = SecretHashRules.NewToken(18);
        share.LinkIssuedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(memberKey, cancellationToken));
    }
}
