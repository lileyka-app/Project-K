using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

/// <summary>
/// Every administrator hears of a kurin's request to change its гілки twice, as of an application:
/// the bell in the app and a letter. Neither may fail the request — it is saved, and the kurin is not
/// the one to tell about a mail outage.
/// </summary>
public sealed class KurinBranchRequestSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<KurinBranchRequestSubmitted>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly INotificationService _notifications;
    private readonly IEmailService _emailService;
    private readonly ILogger<KurinBranchRequestSubmittedEventHandler> _logger;

    public KurinBranchRequestSubmittedEventHandler(
        UserManager<AppUser> userManager,
        INotificationService notifications,
        IEmailService emailService,
        ILogger<KurinBranchRequestSubmittedEventHandler> logger)
    {
        _userManager = userManager;
        _notifications = notifications;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<KurinBranchRequestSubmitted> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        try
        {
            var admins = await _userManager.GetUsersInRoleAsync(SystemRole.Admin);

            await _notifications.NotifyManyAsync(
                admins.Select(admin => new NotificationRequest
                {
                    RecipientUserKey = admin.Id,
                    Type = AppNotificationType.KurinBranchRequestSubmitted,
                    Title = "Запит на зміну гілок куреня",
                    Body = $"{raised.Kurin} → {raised.Wanted}",
                    EntityType = "KurinBranchRequest",
                    EntityKey = raised.KurinBranchRequestKey,
                    Route = "/waitlist",
                    ActorUserKey = raised.ActorUserKey,
                    DeduplicationKey = $"kurin-branch-request:{raised.KurinBranchRequestKey}:{admin.Id}"
                }),
                cancellationToken);

            foreach (var admin in admins.Where(admin => !string.IsNullOrWhiteSpace(admin.Email)))
            {
                await _emailService.SendKurinBranchRequestEmailAsync(admin.Email!, raised.Kurin, raised.Wanted, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Kurin branch request {RequestKey} saved, but administrators could not be told", raised.KurinBranchRequestKey);
        }
    }
}

/// <summary>
/// The one who sent the request hears how it was settled. Runs after the decision is saved, so a
/// failed notification must not turn an applied decision into an error for the administrator.
/// </summary>
public sealed class KurinBranchRequestReviewedEventHandler
    : INotificationHandler<DomainEventNotification<KurinBranchRequestReviewed>>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<KurinBranchRequestReviewedEventHandler> _logger;

    public KurinBranchRequestReviewedEventHandler(INotificationService notifications, ILogger<KurinBranchRequestReviewedEventHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<KurinBranchRequestReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        try
        {
            await Notify(raised, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Kurin branch request {RequestKey} settled, but the requester could not be told", raised.KurinBranchRequestKey);
        }
    }

    private Task Notify(KurinBranchRequestReviewed raised, CancellationToken cancellationToken)
        => _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.RequesterUserKey,
                Type = AppNotificationType.KurinBranchRequestReviewed,
                Severity = raised.Approved ? AppNotificationSeverity.Success : AppNotificationSeverity.Info,
                Title = raised.Approved ? "Гілки куреня змінено" : "Запит на зміну гілок відхилено",
                Body = raised.Branches,
                EntityType = "KurinBranchRequest",
                EntityKey = raised.KurinBranchRequestKey,
                Route = $"/kurin/{raised.KurinKey}/settings",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"kurin-branch-request-reviewed:{raised.KurinBranchRequestKey}"
            },
            cancellationToken);
}
