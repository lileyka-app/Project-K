using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Membership;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <inheritdoc />
public sealed class MemberActivationWriter : IMemberActivationWriter
{
    private readonly ISender _sender;

    public MemberActivationWriter(ISender sender)
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
