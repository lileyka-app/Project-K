using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class KurinFounderWriter : IKurinFounderWriter
{
    private readonly ISender _sender;

    public KurinFounderWriter(ISender sender)
    {
        _sender = sender;
    }

    public async Task JoinFoundedKurinAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        await _sender.Send(new JoinKurinCommand(memberKey, kurinKey, GroupKey: null), cancellationToken);
    }

    public async Task SeatFounderAsync(Guid memberKey, Guid kurinKey, CancellationToken cancellationToken = default)
    {
        await _sender.Send(new UpsertLeadershipCommand(new UpsertLeadershipRequest
        {
            Type = LeadershipType.KV.ToString(),
            EntityKey = kurinKey,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            LeadershipHistories = new[]
            {
                new LeadershipHistoryMemberDto
                {
                    Role = LeadershipRole.Zvyazkovyi.ToString(),
                    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Member = new MemberLookupDto { MemberKey = memberKey }
                }
            }
        })
        {
            // The activating user is still anonymous here, so there is no assigner to check.
            SeatedBySystem = true
        }, cancellationToken);
    }
}
