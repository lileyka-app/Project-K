using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Security;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>Makes a permanent card link, replacing any earlier one: the old link stops working.</summary>
public sealed record IssueMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

public sealed class IssueMyCardLinkCommandHandler : IRequestHandler<IssueMyCardLinkCommand, ServiceResult<MyCardDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public IssueMyCardLinkCommandHandler(MePerson me, MemberCards cards, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(IssueMyCardLinkCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return MeErrors.NoMember<MyCardDto>();
        }

        var memberKey = me.Person.MemberKey;
        if (!(await _cards.AllowanceAsync(memberKey, cancellationToken)).MayLink)
        {
            return ServiceResult<MyCardDto>.Failure(
                ResultType.Forbidden, MeErrors.CardLinkNotAllowed, "A permanent link is not allowed for this card.");
        }

        var share = await _cards.ShareOfAsync(memberKey, cancellationToken);
        share.LinkToken = SecretHash.NewToken(18);
        share.LinkIssuedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(memberKey, cancellationToken));
    }
}
