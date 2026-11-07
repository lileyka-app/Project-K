using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

/// <summary>
/// The danger zone is told to the kurin's other Звʼязкові and its курінний — those who stand in the
/// kurin now. An office row left behind by someone who has gone is history, not a seat.
/// </summary>
public class ArchiveNotificationHandlersTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _otherZvyazkovyi = Guid.NewGuid();
    private readonly Guid _kurinnyi = Guid.NewGuid();
    private readonly Guid _goneZvyazkovyi = Guid.NewGuid();

    [Fact]
    public async Task OnlyLeadersStandingInTheKurin_AreTold_AndNotTheOneWhoTurnedItOn()
    {
        var leaderships = new Mock<ILeadershipRepository>();
        var zvyazkovyi = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
        var kurinnyi = SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy);
        leaderships.Setup(l => l.GetOfficeMembersLookupAsync(_kurinKey, LeadershipType.KV, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MemberLookupDto { UserKey = _actor, UserRole = zvyazkovyi },
                new MemberLookupDto { UserKey = _otherZvyazkovyi, UserRole = zvyazkovyi },
                new MemberLookupDto { UserKey = _goneZvyazkovyi, UserRole = zvyazkovyi }
            ]);
        leaderships.Setup(l => l.GetOfficeMembersLookupAsync(_kurinKey, LeadershipType.Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberLookupDto { UserKey = _kurinnyi, UserRole = kurinnyi }]);
        var memberships = new Mock<IMembershipRepository>();
        memberships.Setup(m => m.GetAccountKeysInKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_actor, _otherZvyazkovyi, _kurinnyi]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Leaderships).Returns(leaderships.Object);
        unitOfWork.Setup(u => u.Memberships).Returns(memberships.Object);

        IEnumerable<NotificationRequest> sent = [];
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.NotifyManyAsync(It.IsAny<IEnumerable<NotificationRequest>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<NotificationRequest> requests, CancellationToken _) => sent = requests.ToList())
            .ReturnsAsync([]);

        await new ArchiveDangerZoneEnteredEventHandler(notifications.Object, unitOfWork.Object, NullLogger<ArchiveDangerZoneEnteredEventHandler>.Instance)
            .Handle(new DomainEventNotification<ArchiveDangerZoneEntered>(new ArchiveDangerZoneEntered(_kurinKey, "Ірина", _actor)), CancellationToken.None);

        sent.Select(request => request.RecipientUserKey).Should().BeEquivalentTo([_otherZvyazkovyi, _kurinnyi]);
    }
}
