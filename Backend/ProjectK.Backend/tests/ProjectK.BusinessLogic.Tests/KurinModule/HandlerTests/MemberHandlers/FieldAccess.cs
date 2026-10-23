using Moq;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Members;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

/// <summary>Stand-ins for <see cref="IMemberFieldAccess"/> in handler tests.</summary>
internal static class FieldAccess
{
    /// <summary>A viewer who sees every group of everyone.</summary>
    public static IMemberFieldAccess SeesEverything(Guid? viewerUserKey = null) =>
        Of(new MemberFieldMask(
            viewerUserKey,
            MemberDataFields.All.ToDictionary(info => info.Group, _ => CapabilityReach.Kurin),
            []));

    /// <summary>A viewer who sees nothing of anyone but themselves.</summary>
    public static IMemberFieldAccess SeesNothing(Guid? viewerUserKey = null) =>
        Of(new MemberFieldMask(viewerUserKey, new Dictionary<MemberDataGroup, CapabilityReach>(), []));

    public static IMemberFieldAccess Of(MemberFieldMask mask)
    {
        var access = new Mock<IMemberFieldAccess>();
        access.Setup(a => a.ForViewerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mask);
        return access.Object;
    }
}
