using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Dtos.MeModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>Turns the permanent card link off.</summary>
public sealed record DisableMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

public sealed class DisableMyCardLinkCommandHandler : IRequestHandler<DisableMyCardLinkCommand, ServiceResult<MyCardDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly IUnitOfWork _unitOfWork;

    public DisableMyCardLinkCommandHandler(MePerson me, MemberCards cards, IUnitOfWork unitOfWork)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(DisableMyCardLinkCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return MeErrors.NoMember<MyCardDto>();
        }

        if (await _unitOfWork.MemberCardShares.GetForMemberAsync(me.Person.MemberKey, cancellationToken) is { LinkToken: not null } share)
        {
            share.LinkToken = null;
            share.LinkIssuedAtUtc = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(me.Person.MemberKey, cancellationToken));
    }
}
