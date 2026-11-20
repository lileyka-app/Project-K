using Moq;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>A kurin nobody has asked about anyone: closing a membership finds no request to lapse.</summary>
public static class NoMembershipRequests
{
    public static IMembershipRequestRepository Repository()
    {
        var requests = new Mock<IMembershipRequestRepository>();
        requests
            .Setup(r => r.GetPendingFromAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MembershipRequest>());
        return requests.Object;
    }
}
