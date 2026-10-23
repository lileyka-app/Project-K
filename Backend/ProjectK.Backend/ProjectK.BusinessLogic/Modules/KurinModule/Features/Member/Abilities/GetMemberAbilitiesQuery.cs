using MediatR;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Abilities;

/// <summary>What the caller may do to one member under the kurin's policy, level by level.</summary>
public sealed record GetMemberAbilitiesQuery(Guid MemberKey) : IRequest<ServiceResult<MemberAbilitiesDto>>;

public sealed class GetMemberAbilitiesQueryHandler : IRequestHandler<GetMemberAbilitiesQuery, ServiceResult<MemberAbilitiesDto>>
{
    private static readonly (KurinCapability Capability, int Level)[] WarningLevels =
    [
        (KurinCapability.GiveWarning1, 1), (KurinCapability.GiveWarning2, 2), (KurinCapability.GiveWarning3, 3)
    ];

    private static readonly (KurinCapability Capability, int Level)[] AwardLevels =
    [
        (KurinCapability.ConfirmAward1, 1), (KurinCapability.ConfirmAward2, 2),
        (KurinCapability.ConfirmAward3, 3), (KurinCapability.ConfirmAward4, 4)
    ];

    private readonly IResourceAccessService _access;

    public GetMemberAbilitiesQueryHandler(IResourceAccessService access)
    {
        _access = access;
    }

    public async Task<ServiceResult<MemberAbilitiesDto>> Handle(GetMemberAbilitiesQuery request, CancellationToken cancellationToken)
    {
        var reachable = await _access.ReachableCapabilitiesAsync(ResourceType.Member, request.MemberKey, cancellationToken);

        var give = Levels(WarningLevels, reachable);

        // The issuer may take back what they gave while they could still give it, so anyone who may
        // give some level may cancel at least their own.
        var cancel = reachable.Contains(KurinCapability.CancelWarning) ? WarningCancelAbility.Any
            : give.Count > 0 ? WarningCancelAbility.OwnIssued
            : WarningCancelAbility.None;

        return new ServiceResult<MemberAbilitiesDto>(
            ResultType.Success,
            new MemberAbilitiesDto(
                new WarningAbilitiesDto(give, cancel),
                new AwardAbilitiesDto(Levels(AwardLevels, reachable), reachable.Contains(KurinCapability.RemoveAward)),
                new ProbeAbilitiesDto(reachable.Contains(KurinCapability.SignProbe), reachable.Contains(KurinCapability.UnsignProbe)),
                new BadgeAbilitiesDto(reachable.Contains(KurinCapability.ReviewBadge)),
                reachable.Contains(KurinCapability.ReadJournal)));
    }

    private static IReadOnlyList<int> Levels(
        (KurinCapability Capability, int Level)[] levels,
        IReadOnlySet<KurinCapability> reachable) =>
        levels.Where(level => reachable.Contains(level.Capability)).Select(level => level.Level).ToList();
}
