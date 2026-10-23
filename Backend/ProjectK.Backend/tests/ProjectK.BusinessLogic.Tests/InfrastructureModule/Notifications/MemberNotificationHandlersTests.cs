using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

/// <summary>
/// The wording, the severity and the route now live with the inbox rather than with the use case
/// that raised the event, so this is where they are pinned.
/// </summary>
public class MemberNotificationHandlersTests
{
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IReviewNotificationRecipientResolver> _recipients = new();

    private static DomainEventNotification<TEvent> Raised<TEvent>(TEvent domainEvent)
        where TEvent : ProjectK.Common.Interfaces.IDomainEvent
        => new(domainEvent);


    private MemberProfileWentStaleEventHandler StaleHandler(params (Membership Membership, bool VerificationOn)[] memberships)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var membershipRepository = new Mock<IMembershipRepository>();
        var kurinRepository = new Mock<IKurinRepository>();
        membershipRepository
            .Setup(x => x.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships.Select(m => m.Membership).ToList());
        foreach (var (membership, verificationOn) in memberships)
        {
            kurinRepository
                .Setup(x => x.GetByKeyAsync(membership.KurinKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Kurin(1) { KurinKey = membership.KurinKey, ProfileVerificationEnabled = verificationOn });
        }
        unitOfWork.SetupGet(x => x.Memberships).Returns(membershipRepository.Object);
        unitOfWork.SetupGet(x => x.Kurins).Returns(kurinRepository.Object);
        return new MemberProfileWentStaleEventHandler(_notifications.Object, _recipients.Object, unitOfWork.Object);
    }

    [Fact]
    public async Task StaleProfile_ShouldAskTheProvidWhoVerifyToCheckItAgain()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var actorUserKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();
        _recipients
            .Setup(x => x.ResolveAsync(It.IsAny<ReviewSubject>(), kurinKey, groupKey, actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mentorUserKey]);
        var handler = StaleHandler((new Membership { MemberKey = memberKey, KurinKey = kurinKey, GroupKey = groupKey }, true));

        await handler.Handle(
            Raised(new MemberProfileWentStale(memberKey, "Іван Петренко", actorUserKey)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Single().RecipientUserKey == mentorUserKey
                && requests.Single().Type == AppNotificationType.MemberProfileChangedAfterVerification
                && requests.Single().Severity == AppNotificationSeverity.Warn
                && requests.Single().Body == "Дані Іван Петренко змінилися після верифікації. Перевір їх ще раз."
                && requests.Single().EntityKey == memberKey
                && requests.Single().Route == $"/member/{memberKey}"
                && requests.Single().ActorUserKey == actorUserKey
                && requests.Single().DeduplicationKey == $"member-profile-stale:{memberKey}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StaleProfile_InAKurinWithoutVerification_ShouldSendNothing()
    {
        var handler = StaleHandler((new Membership { MemberKey = Guid.NewGuid(), KurinKey = Guid.NewGuid() }, false));

        await handler.Handle(
            Raised(new MemberProfileWentStale(Guid.NewGuid(), "Іван", null)),
            CancellationToken.None);

        _recipients.Verify(x => x.ResolveAsync(It.IsAny<ReviewSubject>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.NotifyManyAsync(It.IsAny<IEnumerable<NotificationRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifiedProfile_ShouldTellTheOwnerItIsCurrent()
    {
        var memberKey = Guid.NewGuid();
        var ownerUserKey = Guid.NewGuid();
        var handler = new MemberProfileVerifiedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new MemberProfileVerified(memberKey, ownerUserKey, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyAsync(
            It.Is<NotificationRequest>(request =>
                request.RecipientUserKey == ownerUserKey
                && request.Type == AppNotificationType.MemberProfileVerified
                && request.Severity == AppNotificationSeverity.Success
                && request.Route == $"/member/{memberKey}"
                && request.DeduplicationKey == $"member-profile-verified:{memberKey}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(true, AppNotificationSeverity.Success, "Відзначення затверджено")]
    [InlineData(false, AppNotificationSeverity.Warn, "Відзначення не затверджено")]
    public async Task ReviewedAward_ShouldMatchTheOutcome(
        bool isApproved,
        AppNotificationSeverity severity,
        string title)
    {
        var awardKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var ownerUserKey = Guid.NewGuid();
        var handler = new MemberAwardReviewedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new MemberAwardReviewed(awardKey, memberKey, ownerUserKey, isApproved, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyAsync(
            It.Is<NotificationRequest>(request =>
                request.RecipientUserKey == ownerUserKey
                && request.Severity == severity
                && request.Title == title
                && request.EntityKey == awardKey
                && request.Route == $"/member/{memberKey}"
                && request.DeduplicationKey == $"award-review:{awardKey}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmittedAward_ShouldReachEveryReviewerTheResolverNames()
    {
        var awardKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var actorUserKey = Guid.NewGuid();
        var managerUserKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();

        _recipients
            .Setup(x => x.ResolveAsync(It.IsAny<ReviewSubject>(), kurinKey, groupKey, actorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([managerUserKey, mentorUserKey]);

        var handler = new MemberAwardSubmittedEventHandler(_notifications.Object, _recipients.Object);

        await handler.Handle(
            Raised(new MemberAwardSubmitted(awardKey, memberKey, "Іван Петренко", kurinKey, groupKey, actorUserKey)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Count() == 2
                && requests.Select(request => request.RecipientUserKey)
                    .ToHashSet()
                    .SetEquals(new[] { managerUserKey, mentorUserKey })
                && requests.All(request =>
                    request.Type == AppNotificationType.MemberAwardSubmitted
                    && request.Title == "Відзначення подано на розгляд"
                    && request.Body == "Надійшло відзначення від Іван Петренко на розгляд."
                    && request.EntityKey == awardKey
                    && request.DeduplicationKey == $"award-submitted:{awardKey}")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmittedAward_WithNoReviewers_ShouldSendNothing()
    {
        _recipients
            .Setup(x => x.ResolveAsync(It.IsAny<ReviewSubject>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new MemberAwardSubmittedEventHandler(_notifications.Object, _recipients.Object);

        await handler.Handle(
            Raised(new MemberAwardSubmitted(Guid.NewGuid(), Guid.NewGuid(), "X", Guid.NewGuid(), null, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.IsAny<IEnumerable<NotificationRequest>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(MemberWarningLevel.Level1, "першу пересторогу")]
    [InlineData(MemberWarningLevel.Level2, "другу пересторогу")]
    [InlineData(MemberWarningLevel.Level3, "третю пересторогу")]
    public async Task AssignedWarning_ShouldNameTheLevel(MemberWarningLevel level, string expected)
    {
        var warningKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var ownerUserKey = Guid.NewGuid();
        var handler = new MemberWarningAssignedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new MemberWarningAssigned(warningKey, memberKey, ownerUserKey, level, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyAsync(
            It.Is<NotificationRequest>(request =>
                request.RecipientUserKey == ownerUserKey
                && request.Severity == AppNotificationSeverity.Warn
                && request.Body == $"До вашого профілю додано {expected}."
                && request.EntityKey == warningKey
                && request.DeduplicationKey == $"member-warning:{warningKey}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmittedBadge_ShouldPointReviewersAtTheKurinQueue()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mentorUserKey = Guid.NewGuid();

        _recipients
            .Setup(x => x.ResolveAsync(It.IsAny<ReviewSubject>(), kurinKey, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mentorUserKey]);

        var handler = new BadgeProgressSubmittedEventHandler(_notifications.Object, _recipients.Object);

        await handler.Handle(
            Raised(new BadgeProgressSubmitted(Guid.NewGuid(), "badge-1", memberKey, "Іван", kurinKey, null, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyManyAsync(
            It.Is<IEnumerable<NotificationRequest>>(requests =>
                requests.Single().RecipientUserKey == mentorUserKey
                && requests.Single().Type == AppNotificationType.MemberSkillSubmittedForReview
                && requests.Single().Route == $"/kurin/{kurinKey}/review/skills"
                && requests.Single().DeduplicationKey == $"skill-review:{memberKey}:badge-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(true, false, "Вмілість зараховано")]
    [InlineData(false, true, "Підтвердження вмілості скасовано")]
    [InlineData(false, false, "Вмілість потребує доопрацювання")]
    public async Task ReviewedBadge_ShouldSeparateRejectionFromWithdrawal(
        bool isApproved,
        bool withdrawn,
        string title)
    {
        var memberKey = Guid.NewGuid();
        var ownerUserKey = Guid.NewGuid();
        var handler = new BadgeProgressReviewedEventHandler(_notifications.Object);

        await handler.Handle(
            Raised(new BadgeProgressReviewed(
                Guid.NewGuid(), "badge-1", memberKey, ownerUserKey, isApproved, withdrawn, null)),
            CancellationToken.None);

        _notifications.Verify(x => x.NotifyAsync(
            It.Is<NotificationRequest>(request =>
                request.RecipientUserKey == ownerUserKey
                && request.Title == title
                && request.Route == $"/member/{memberKey}"
                && request.DeduplicationKey == $"skill-review-result:{memberKey}:badge-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
