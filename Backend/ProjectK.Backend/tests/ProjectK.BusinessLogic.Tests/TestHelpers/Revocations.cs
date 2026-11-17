using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>The revocation a security change runs, over a session store the test watches.</summary>
public static class Revocations
{
    public static RefreshTokenInvalidation Of(
        IRefreshTokenStore refreshTokens,
        IDomainEventPublisher? events = null) =>
        new(refreshTokens, events ?? new Mock<IDomainEventPublisher>().Object);
}
