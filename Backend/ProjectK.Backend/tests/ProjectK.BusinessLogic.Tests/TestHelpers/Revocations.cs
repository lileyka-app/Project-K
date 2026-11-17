using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>The revocation a security change runs, over a session store the test watches.</summary>
public static class Revocations
{
    public static RefreshTokenInvalidation Of(
        IRefreshTokenStore refreshTokens,
        ICalendarFeedStore? calendarFeeds = null,
        INotificationService? notifications = null) =>
        new(refreshTokens, calendarFeeds ?? new Mock<ICalendarFeedStore>().Object, notifications ?? new Mock<INotificationService>().Object);
}
