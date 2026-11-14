using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>The person's own card: what they may share, what they chose, the permanent link.</summary>
public sealed record GetMyCardQuery : IRequest<ServiceResult<MyCardDto>>;

public sealed class GetMyCardQueryHandler : IRequestHandler<GetMyCardQuery, ServiceResult<MyCardDto>>
{
    private readonly ICurrentMember _me;
    private readonly MemberCards _cards;

    public GetMyCardQueryHandler(ICurrentMember me, MemberCards cards)
    {
        _me = me;
        _cards = cards;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(GetMyCardQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        return me is null
            ? MeErrors.NoMember<MyCardDto>()
            : new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(me.Person.MemberKey, cancellationToken));
    }
}
