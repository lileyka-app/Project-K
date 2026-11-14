using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>Which parts the card shows. Kept as chosen even beyond what is allowed now, and cut on every read.</summary>
public sealed record SetMyCardFieldsCommand(CardField Fields) : IRequest<ServiceResult<MyCardDto>>;

public sealed class SetMyCardFieldsCommandHandler : IRequestHandler<SetMyCardFieldsCommand, ServiceResult<MyCardDto>>
{
    private readonly ICurrentMember _me;
    private readonly MemberCards _cards;
    private readonly IUnitOfWork _unitOfWork;

    public SetMyCardFieldsCommandHandler(ICurrentMember me, MemberCards cards, IUnitOfWork unitOfWork)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(SetMyCardFieldsCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        if (me is null)
        {
            return MeErrors.NoMember<MyCardDto>();
        }

        var share = await _cards.ShareOfAsync(me.Person.MemberKey, cancellationToken);
        share.Fields = request.Fields & CardField.All;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(me.Person.MemberKey, cancellationToken));
    }
}
