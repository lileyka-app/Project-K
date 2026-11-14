using Moq;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// A resolver that answers with whatever roles a test wants, for the kurin the account is in.
/// Handlers no longer read the identity store, so this is what stands in for "who is asking".
/// </summary>
internal static class FakeAccessContext
{
    internal static Mock<IAccessContextResolver> Resolver(params string[] roles)
    {
        var mock = new Mock<IAccessContextResolver>();
        var answer = roles.Length == 0 ? [SystemRole.Member] : roles;

        mock.Setup(r => r.ResolveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userKey, CancellationToken _) =>
                new AccessContext(userKey, null, answer));

        mock.Setup(r => r.ResolveAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) =>
                new AccessContext(user.Id, user.ResolveScopeKurinKey(), answer));

        return mock;
    }

    internal static IAccessContextResolver With(params string[] roles) => Resolver(roles).Object;
}
