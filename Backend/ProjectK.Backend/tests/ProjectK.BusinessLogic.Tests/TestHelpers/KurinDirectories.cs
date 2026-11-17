using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>The kurin's real directories over a test's unit of work, so its Groups, Kurins and KurinTags setups answer.</summary>
internal static class KurinDirectories
{
    public static IKurinDirectory Over(IUnitOfWork unitOfWork) => new KurinDirectory(unitOfWork);

    /// <summary>A kurin directory that knows no гуртки: a card's offices then come back without one.</summary>
    public static IKurinDirectory WithoutGroups()
    {
        var directory = new Mock<IKurinDirectory>();
        directory.Setup(d => d.GetGroupsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, GroupSummary>());
        return directory.Object;
    }

    public static IKurinTagDirectory TagsOver(IUnitOfWork unitOfWork, IKurinTagVisibility? visibility = null)
        => new KurinTagDirectory(visibility ?? Mock.Of<IKurinTagVisibility>(), unitOfWork);
}
