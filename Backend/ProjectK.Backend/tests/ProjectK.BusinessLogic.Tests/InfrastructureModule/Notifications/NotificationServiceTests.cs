using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

public class NotificationServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAppNotificationRepository> _notificationRepositoryMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IPushQueue> _pushQueueMock;
    private readonly NotificationService _service;

    private readonly Guid _currentUserKey = Guid.NewGuid();

    public NotificationServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _notificationRepositoryMock = new Mock<IAppNotificationRepository>();
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _pushQueueMock = new Mock<IPushQueue>();

        _unitOfWorkMock.SetupGet(x => x.AppNotifications).Returns(_notificationRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _currentUserContextMock.SetupGet(x => x.UserId).Returns(_currentUserKey);

        _service = new NotificationService(
            _unitOfWorkMock.Object,
            _currentUserContextMock.Object,
            _pushQueueMock.Object);
    }

    [Fact]
    public async Task NotifyAsync_ShouldCreateNotification()
    {
        AppNotification? created = null;
        _notificationRepositoryMock
            .Setup(x => x.Create(It.IsAny<AppNotification>(), It.IsAny<CancellationToken>()))
            .Callback<AppNotification, CancellationToken>((notification, _) => created = notification);

        var result = await _service.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.MemberProfileVerified,
            Severity = AppNotificationSeverity.Success,
            Title = "Profile verified",
            Body = "Member profile was verified.",
            EntityType = "member",
            EntityKey = Guid.NewGuid(),
            Route = "/member/test"
        });

        result.Should().NotBeNull();
        created.Should().NotBeNull();
        created!.RecipientUserKey.Should().Be(_currentUserKey);
        created.Type.Should().Be(AppNotificationType.MemberProfileVerified);
        created.Severity.Should().Be(AppNotificationSeverity.Success);
        created.Title.Should().Be("Profile verified");
        created.Body.Should().Be("Member profile was verified.");
        created.Route.Should().Be("/member/test");
        created.ReadAtUtc.Should().BeNull();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_ShouldUpdateExistingUnreadNotificationWithSameDeduplicationKey()
    {
        var existing = new AppNotification
        {
            NotificationKey = Guid.NewGuid(),
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.MemberProfileChangedAfterVerification,
            Title = "Old title",
            Body = "Old body",
            DeduplicationKey = "profile-stale:member-1"
        };

        _notificationRepositoryMock
            .Setup(x => x.GetUnreadByDeduplicationKeyAsync(
                _currentUserKey,
                "profile-stale:member-1",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _service.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.MemberProfileChangedAfterVerification,
            Severity = AppNotificationSeverity.Warn,
            Title = "New title",
            Body = "New body",
            DeduplicationKey = "profile-stale:member-1"
        });

        result.Should().NotBeNull();
        existing.Title.Should().Be("New title");
        existing.Body.Should().Be("New body");
        existing.Severity.Should().Be(AppNotificationSeverity.Warn);
        existing.ReadAtUtc.Should().BeNull();
        _notificationRepositoryMock.Verify(x => x.Create(It.IsAny<AppNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_ShouldQueueANewNotificationForThePhoneOnceSaved()
    {
        var steps = new List<string>();
        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("saved"))
            .ReturnsAsync(1);
        AppNotificationDto? queued = null;
        _pushQueueMock
            .Setup(x => x.Enqueue(It.IsAny<AppNotificationDto>()))
            .Callback<AppNotificationDto>(dto =>
            {
                steps.Add("queued");
                queued = dto;
            });

        var result = await _service.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.AgendaItemAssigned,
            Title = "Задачу призначено вам",
            Body = "Принести намет",
            Route = "/tasks/k1"
        });

        steps.Should().Equal("saved", "queued");
        queued.Should().BeSameAs(result);
        queued!.RecipientUserKey.Should().Be(_currentUserKey);
        queued.Title.Should().Be("Задачу призначено вам");
    }

    [Fact]
    public async Task NotifyAsync_ShouldNotBuzzThePhoneAgainForARepeatFoldedIntoAnUnreadOne()
    {
        _notificationRepositoryMock
            .Setup(x => x.GetUnreadByDeduplicationKeyAsync(
                _currentUserKey,
                "agenda-assigned:item-1",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppNotification
            {
                RecipientUserKey = _currentUserKey,
                Type = AppNotificationType.AgendaItemAssigned,
                DeduplicationKey = "agenda-assigned:item-1"
            });

        var result = await _service.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.AgendaItemAssigned,
            Title = "Задачу призначено вам",
            Body = "Принести намет",
            DeduplicationKey = "agenda-assigned:item-1"
        });

        result.Should().NotBeNull();
        _pushQueueMock.Verify(x => x.Enqueue(It.IsAny<AppNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_ShouldQueueNothingWhenNothingWasSaved()
    {
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _service.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.MemberWarningAssigned,
            Title = "Пересторога",
            Body = "-"
        });

        result.Should().BeNull();
        _pushQueueMock.Verify(x => x.Enqueue(It.IsAny<AppNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task GetInboxAsync_ShouldQueryCurrentUserInbox()
    {
        var notifications = new List<AppNotification>
        {
            new()
            {
                NotificationKey = Guid.NewGuid(),
                RecipientUserKey = _currentUserKey,
                Type = AppNotificationType.MemberWarningAssigned,
                Title = "Warning",
                Body = "Warning assigned."
            }
        };

        _notificationRepositoryMock
            .Setup(x => x.GetInboxAsync(
                _currentUserKey,
                true,
                25,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var result = await _service.GetInboxAsync(new NotificationQuery
        {
            UnreadOnly = true,
            Take = 25
        });

        result.Should().ContainSingle();
        result[0].NotificationKey.Should().Be(notifications[0].NotificationKey);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldSetReadAtOnlyForCurrentUserNotification()
    {
        var notification = new AppNotification
        {
            NotificationKey = Guid.NewGuid(),
            RecipientUserKey = _currentUserKey,
            Type = AppNotificationType.MemberAwardReviewed,
            Title = "Award reviewed",
            Body = "Award was reviewed."
        };

        _notificationRepositoryMock
            .Setup(x => x.GetByRecipientAndKeyAsync(
                _currentUserKey,
                notification.NotificationKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var result = await _service.MarkAsReadAsync(notification.NotificationKey);

        result.Should().NotBeNull();
        notification.ReadAtUtc.Should().NotBeNull();
        _notificationRepositoryMock.Verify(x => x.Update(notification, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldDelegateToRepositoryForCurrentUser()
    {
        _notificationRepositoryMock
            .Setup(x => x.MarkAllAsReadAsync(
                _currentUserKey,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _service.MarkAllAsReadAsync();

        result.Should().Be(3);
    }
}
