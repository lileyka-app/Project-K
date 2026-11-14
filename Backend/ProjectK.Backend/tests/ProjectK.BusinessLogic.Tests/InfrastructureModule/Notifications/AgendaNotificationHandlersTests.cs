using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

/// <summary>
/// The agenda states who its change reaches; this is where that turns into wording and a screen.
/// A задача belongs to the board and a подія to the calendar — the routes were never covered before
/// the wording moved here.
/// </summary>
public class AgendaNotificationHandlersTests
{
    private readonly Mock<INotificationService> _notifications = new();

    private static DomainEventNotification<TEvent> Raised<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent
        => new(domainEvent);

    [Theory]
    [InlineData(AgendaItemKind.Task, "Задачу призначено вам", "/tasks/")]
    [InlineData(AgendaItemKind.Event, "Подію призначено вам", "/calendar/")]
    public async Task Assigned_ShouldAddressEveryRecipientAndPointAtTheRightScreen(
        AgendaItemKind kind,
        string title,
        string routePrefix)
    {
        var itemKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var handler = new AgendaItemAssignedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new AgendaItemAssigned(itemKey, kurinKey, kind, "Прибирання оселі", [first, second], actor)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Count() == 2
                && requests.All(request =>
                    request.Type == AppNotificationType.AgendaItemAssigned
                    && request.Severity == AppNotificationSeverity.Info
                    && request.Title == title
                    && request.Body == "Прибирання оселі"
                    && request.EntityType == "AgendaItem"
                    && request.EntityKey == itemKey
                    && request.Route == routePrefix + kurinKey
                    && request.ActorUserKey == actor)
                && requests.Select(request => request.DeduplicationKey)
                    .ToHashSet()
                    .SetEquals(new[]
                    {
                        $"agenda-assigned:{itemKey}:{first}",
                        $"agenda-assigned:{itemKey}:{second}"
                    })),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Assigned_WithNobodyToTell_ShouldSendNothing()
    {
        var handler = new AgendaItemAssignedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new AgendaItemAssigned(
                Guid.NewGuid(), Guid.NewGuid(), AgendaItemKind.Task, "x", [], Guid.NewGuid())),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.IsAny<IEnumerable<NotificationRequest>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Changed_ShouldTellTheAssignedThatSomethingMoved()
    {
        var itemKey = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var handler = new AgendaItemChangedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new AgendaItemChanged(
                itemKey, Guid.NewGuid(), AgendaItemKind.Task, "Збірка", [recipient], Guid.NewGuid())),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Single().RecipientUserKey == recipient
                && requests.Single().Type == AppNotificationType.AgendaItemUpdated
                && requests.Single().Title == "Оновлено призначення"
                && requests.Single().DeduplicationKey == $"agenda-updated:{itemKey}:{recipient}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Removed_ShouldWarnThePeopleWhoWereExpectingIt()
    {
        var itemKey = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        var handler = new AgendaItemRemovedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new AgendaItemRemoved(
                itemKey, Guid.NewGuid(), AgendaItemKind.Event, "Прогулянка", [recipient], Guid.NewGuid())),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Single().RecipientUserKey == recipient
                && requests.Single().Type == AppNotificationType.AgendaItemDeleted
                && requests.Single().Severity == AppNotificationSeverity.Warn
                && requests.Single().Title == "Подію видалено"
                && requests.Single().DeduplicationKey == $"agenda-deleted:{itemKey}:{recipient}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(AgendaItemStatus.Done, AppNotificationSeverity.Success)]
    [InlineData(AgendaItemStatus.InProgress, AppNotificationSeverity.Info)]
    public async Task StatusChanged_ShouldReachTheCreatorWithSeverityMatchingTheColumn(
        AgendaItemStatus status,
        AppNotificationSeverity severity)
    {
        var itemKey = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var handler = new AgendaItemStatusChangedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new AgendaItemStatusChanged(
                itemKey, Guid.NewGuid(), AgendaItemKind.Task, "Звіт", status, creator, Guid.NewGuid())),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyAsync(
            It.Is<NotificationRequest>(request =>
                request.RecipientUserKey == creator
                && request.Type == AppNotificationType.AgendaItemStatusChanged
                && request.Severity == severity
                && request.Title == "Статус задачі змінено"
                && request.DeduplicationKey == $"agenda-status:{itemKey}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
