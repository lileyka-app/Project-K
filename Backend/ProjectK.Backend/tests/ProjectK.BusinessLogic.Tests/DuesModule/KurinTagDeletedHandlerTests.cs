using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Events;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.DuesModule;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>A deleted tag's rates leave no unpaid quarter behind: the whole kurin is repriced from the start.</summary>
public class KurinTagDeletedHandlerTests
{
    [Fact]
    public async Task RepricesEveryUnpaidQuarterOfTheKurin()
    {
        var kurinKey = Guid.NewGuid();
        var accrual = new Mock<IDuesAccrual>();

        await new KurinTagDeletedEventHandler(accrual.Object)
            .Handle(new DomainEventNotification<KurinTagDeleted>(new KurinTagDeleted(kurinKey, Guid.NewGuid())), CancellationToken.None);

        accrual.Verify(a => a.RepriceKurinAsync(kurinKey, DuesQuarter.FromIndex(0), It.IsAny<CancellationToken>()), Times.Once);
    }
}
