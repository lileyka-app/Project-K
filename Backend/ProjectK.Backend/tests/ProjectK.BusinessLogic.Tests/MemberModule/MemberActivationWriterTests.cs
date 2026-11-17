using FluentAssertions;
using MediatR;
using Moq;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Tests.MemberModule;

/// <summary>
/// What an activation that founds a kurin asks of the member module. Seating the founder is the one
/// place where getting authorization wrong locks a kurin's first leader out of their own kurin, because
/// the office is seated while the activating caller is still anonymous.
/// </summary>
public class MemberActivationWriterTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly MemberActivationWriter _writer;

    public MemberActivationWriterTests()
    {
        _writer = new MemberActivationWriter(_sender.Object);
    }

    [Fact]
    public async Task JoinsTheFoundedKurinWithoutAGroup()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        JoinKurinCommand? joined = null;
        _sender
            .Setup(x => x.Send(It.IsAny<JoinKurinCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => joined = (JoinKurinCommand)request)
            .ReturnsAsync(new ServiceResult<Guid>(ResultType.Created));

        await _writer.JoinFoundedKurinAsync(memberKey, kurinKey);

        joined.Should().Be(new JoinKurinCommand(memberKey, kurinKey, GroupKey: null));
    }

    /// <summary>
    /// The regression this file exists for. The office must be seated with SeatedBySystem, because the
    /// activating user is anonymous and there is nobody to authorize the assignment against — without
    /// it a kurin's first leader activates into a kurin they cannot manage.
    /// </summary>
    [Fact]
    public async Task SeatsTheFounderAsTheSystem()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        UpsertLeadershipCommand? seated = null;
        _sender
            .Setup(x => x.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => seated = (UpsertLeadershipCommand)request)
            .ReturnsAsync(new ServiceResult<LeadershipDto>(ResultType.Success));

        await _writer.SeatFounderAsync(memberKey, kurinKey);

        seated.Should().NotBeNull();
        seated!.SeatedBySystem.Should().BeTrue(
            "the activating user is anonymous, so nothing else can authorize the assignment");
        seated.EntityKey.Should().Be(kurinKey);
        seated.LeadershipHistoryMembers.Should().ContainSingle(history =>
            history.Role == LeadershipRole.Zvyazkovyi.ToString() && history.Member!.MemberKey == memberKey);
    }
}
