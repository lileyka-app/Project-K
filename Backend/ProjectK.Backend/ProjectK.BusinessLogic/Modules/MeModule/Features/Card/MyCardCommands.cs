using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Security;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Card;

/// <summary>The person's own card: what they may share, what they chose, the permanent link.</summary>
public sealed record GetMyCardQuery : IRequest<ServiceResult<MyCardDto>>;

/// <summary>Which parts the card shows. Kept as chosen even beyond what is allowed now, and cut on every read.</summary>
public sealed record SetMyCardFieldsCommand(CardField Fields) : IRequest<ServiceResult<MyCardDto>>;

/// <summary>Makes a permanent card link, replacing any earlier one: the old link stops working.</summary>
public sealed record IssueMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

/// <summary>Turns the permanent card link off.</summary>
public sealed record DisableMyCardLinkCommand : IRequest<ServiceResult<MyCardDto>>;

/// <summary>A QR for checking the card that works for <see cref="MemberCards.CheckLifetime"/>.</summary>
public sealed record GetMyCardCheckCodeQuery : IRequest<ServiceResult<CardCodeDto>>;

/// <summary>
/// The transfer code as a QR: a link that opens «Склад куреня» with the code already typed in, for
/// the Звʼязковий of the kurin the person is going to.
/// </summary>
public sealed record GetMyTransferCodeQuery : IRequest<ServiceResult<CardCodeDto>>;

public sealed class GetMyCardQueryHandler : IRequestHandler<GetMyCardQuery, ServiceResult<MyCardDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;

    public GetMyCardQueryHandler(MePerson me, MemberCards cards)
    {
        _me = me;
        _cards = cards;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(GetMyCardQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        return me is null
            ? new ServiceResult<MyCardDto>(ResultType.Forbidden)
            : new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(me.Person.MemberKey, cancellationToken));
    }
}

public sealed class SetMyCardFieldsCommandHandler : IRequestHandler<SetMyCardFieldsCommand, ServiceResult<MyCardDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly IUnitOfWork _unitOfWork;

    public SetMyCardFieldsCommandHandler(MePerson me, MemberCards cards, IUnitOfWork unitOfWork)
    {
        _me = me;
        _cards = cards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MyCardDto>> Handle(SetMyCardFieldsCommand request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<MyCardDto>(ResultType.Forbidden);
        }

        var share = await _cards.ShareOfAsync(me.Person.MemberKey, cancellationToken);
        share.Fields = request.Fields & CardField.All;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(me.Person.MemberKey, cancellationToken));
    }
}

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
            return new ServiceResult<MyCardDto>(ResultType.Forbidden);
        }

        var memberKey = me.Person.MemberKey;
        if (!(await _cards.AllowanceAsync(memberKey, cancellationToken)).MayLink)
        {
            return ServiceResult<MyCardDto>.Failure(
                ResultType.Forbidden, "CardLinkNotAllowed", "A permanent link is not allowed for this card.");
        }

        var share = await _cards.ShareOfAsync(memberKey, cancellationToken);
        share.LinkToken = SecretHash.NewToken(18);
        share.LinkIssuedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MyCardDto>(ResultType.Success, await _cards.MineAsync(memberKey, cancellationToken));
    }
}

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
            return new ServiceResult<MyCardDto>(ResultType.Forbidden);
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

public sealed class GetMyCardCheckCodeQueryHandler : IRequestHandler<GetMyCardCheckCodeQuery, ServiceResult<CardCodeDto>>
{
    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly ICardTokenSigner _signer;
    private readonly IQrCodeService _qrCodes;
    private readonly TimeProvider _time;

    public GetMyCardCheckCodeQueryHandler(MePerson me, MemberCards cards, ICardTokenSigner signer, IQrCodeService qrCodes, TimeProvider time)
    {
        _me = me;
        _cards = cards;
        _signer = signer;
        _qrCodes = qrCodes;
        _time = time;
    }

    public async Task<ServiceResult<CardCodeDto>> Handle(GetMyCardCheckCodeQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<CardCodeDto>(ResultType.Forbidden);
        }

        if (!(await _cards.AllowanceAsync(me.Person.MemberKey, cancellationToken)).MayShare)
        {
            return ServiceResult<CardCodeDto>.Failure(
                ResultType.Forbidden, "CardSharingNotAllowed", "This card may not be shared.");
        }

        var expires = _time.GetUtcNow().UtcDateTime + MemberCards.CheckLifetime;
        var url = _cards.SiteUrl(MemberCards.CheckPath + _signer.Sign(me.Person.MemberKey, expires));
        return new ServiceResult<CardCodeDto>(ResultType.Success, new CardCodeDto(url, _qrCodes.ToPngDataUrl(url), expires));
    }
}

public sealed class GetMyTransferCodeQueryHandler : IRequestHandler<GetMyTransferCodeQuery, ServiceResult<CardCodeDto>>
{
    public const string TransferPath = "/kurin/transfers?code=";

    private readonly MePerson _me;
    private readonly MemberCards _cards;
    private readonly IMemberDirectory _members;
    private readonly IQrCodeService _qrCodes;

    public GetMyTransferCodeQueryHandler(MePerson me, MemberCards cards, IMemberDirectory members, IQrCodeService qrCodes)
    {
        _me = me;
        _cards = cards;
        _members = members;
        _qrCodes = qrCodes;
    }

    public async Task<ServiceResult<CardCodeDto>> Handle(GetMyTransferCodeQuery request, CancellationToken cancellationToken)
    {
        var me = await _me.ReadAsync(cancellationToken);
        if (me is null)
        {
            return new ServiceResult<CardCodeDto>(ResultType.Forbidden);
        }

        if (await _members.GetPublicIdAsync(me.Person.MemberKey, cancellationToken) is not { } publicId)
        {
            return new ServiceResult<CardCodeDto>(ResultType.NotFound);
        }

        var url = _cards.SiteUrl(TransferPath + publicId);
        return new ServiceResult<CardCodeDto>(ResultType.Success, new CardCodeDto(url, _qrCodes.ToPngDataUrl(url), null));
    }
}
