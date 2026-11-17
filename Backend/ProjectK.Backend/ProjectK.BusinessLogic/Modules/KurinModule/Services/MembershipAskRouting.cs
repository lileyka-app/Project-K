using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

public sealed class MembershipAskRouting
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;

    public MembershipAskRouting(IUnitOfWork unitOfWork, IMemberDirectory members)
    {
        _unitOfWork = unitOfWork;
        _members = members;
    }

    /// <summary>The route as the asking kurin's screen reads it, with the kurins to choose from named.</summary>
    public async Task<MembershipAskOfferDto> OfferAsync(Guid memberKey, Guid toKurinKey, CancellationToken cancellationToken)
    {
        var route = await ResolveAsync(memberKey, toKurinKey, cancellationToken);
        var kurins = new List<MembershipRequestKurinDto>(route.AskableKurinKeys.Count);
        foreach (var kurinKey in route.AskableKurinKeys)
        {
            kurins.Add((await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken)).ToRequestKurinDto(kurinKey));
        }

        return new MembershipAskOfferDto(route.ToSenior, route.JoinsAtOnce, route.PersonAnswers, route.TransferAllowed, kurins);
    }

    public async Task<MembershipAskRoute> ResolveAsync(Guid memberKey, Guid toKurinKey, CancellationToken cancellationToken)
    {
        var memberUserKey = (await _members.FindAsync(memberKey, cancellationToken))?.UserKey;
        var toKurin = await _unitOfWork.Kurins.GetByKeyAsync(toKurinKey, cancellationToken);
        var toSenior = toKurin is not null && KurinBranchSetRules.IsSenior(toKurin.Branch);

        var open = (await _unitOfWork.Memberships.GetRecordsForMemberAsync(memberKey, cancellationToken))
            .Where(m => m.LeftAtUtc is null && m.KurinKey != toKurinKey)
            .ToList();

        if (open.Count == 0)
        {
            // Someone who has left every kurin decides for themselves where they go next as a senior;
            // a youth kurin takes them in at once, as before.
            return toSenior && memberUserKey.HasValue
                ? new MembershipAskRoute(toSenior, JoinsAtOnce: false, PersonAnswers: true, TransferAllowed: false, [])
                : new MembershipAskRoute(toSenior, JoinsAtOnce: true, PersonAnswers: false, TransferAllowed: false, []);
        }

        var sameLine = open.Where(m => KurinBranchSetRules.IsSenior(m.Branch) == toSenior).Select(m => m.KurinKey).Distinct().ToList();
        if (sameLine.Count > 0)
        {
            return new MembershipAskRoute(toSenior, JoinsAtOnce: false, PersonAnswers: false, TransferAllowed: true, sameLine);
        }

        var youthIn = open
            .Where(m => !KurinBranchSetRules.IsSenior(m.Branch) && m.Kind == MembershipKind.Youth)
            .Select(m => m.KurinKey).Distinct().ToList();
        if (toSenior && youthIn.Count > 0)
        {
            return new MembershipAskRoute(toSenior, JoinsAtOnce: false, PersonAnswers: false, TransferAllowed: false, youthIn);
        }

        return memberUserKey.HasValue
            ? new MembershipAskRoute(toSenior, JoinsAtOnce: false, PersonAnswers: true, TransferAllowed: false, [])
            // Without an account there is nobody to answer but the kurin they stand in.
            : new MembershipAskRoute(toSenior, JoinsAtOnce: false, PersonAnswers: false, TransferAllowed: false,
                open.Select(m => m.KurinKey).Distinct().ToList());
    }
}
