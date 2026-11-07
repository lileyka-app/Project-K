using Moq;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces.Modules.KurinModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>A kurin that has defined no tags and put none on anyone: every read answers empty.</summary>
public static class NoKurinTags
{
    public static IKurinTagRepository Repository()
    {
        var tags = new Mock<IKurinTagRepository>();
        tags.Setup(t => t.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        tags.Setup(t => t.GetAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        tags.Setup(t => t.GetAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberTag>());
        tags.Setup(t => t.GetMemberKeysAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return tags.Object;
    }
}
