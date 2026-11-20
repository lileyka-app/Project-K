using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Card;

/// <summary>Turns the permanent card link off.</summary>
public sealed record DisableMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

public sealed class DisableMyCardLinkCommandHandler : IRequestHandler<DisableMyCardLinkCommand, ServiceResult<MyCardDto>>
{
    private readonly ICurrentMember _me;
    private readonly MemberCardService _cards;
    private readonly IUnitOfWork _unitOfWork;

    public DisableMyCardLinkCommandHandler(ICurrentMember me, MemberCardService cards, IUnitOfWork unitOfWork)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(DisableMyCardLinkCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadPersonAsync(cancellationToken);
        if (me is null)
        {
            return MemberErrors.NoMember<MyCardDto>();
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
