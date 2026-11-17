using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

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
            var admins = await _userManager.GetUsersInRoleAsync(SystemRoleNames.Admin);

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
