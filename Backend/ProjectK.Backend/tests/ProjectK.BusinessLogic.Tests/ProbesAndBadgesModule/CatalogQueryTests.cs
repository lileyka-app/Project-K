using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

public class CatalogQueryTests
{
    [Theory]
    [InlineData(0, 200)]
    [InlineData(-5, 200)]
    [InlineData(50, 50)]
    [InlineData(10_000, 500)]
    public async Task GetBadges_ShouldAnswerTheDefaultPage_ForANonPositiveTake_AndCapALargeOne(int take, int expectedPage)
    {
        var catalog = new Mock<IBadgesCatalogService>();
        catalog.Setup(c => c.GetBadges(It.IsAny<int>())).Returns([]);

        var result = await new GetBadgesQueryHandler(catalog.Object).Handle(new GetBadgesQuery(take), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        catalog.Verify(c => c.GetBadges(expectedPage));
    }

    [Fact]
    public async Task GetBadge_ShouldAnswerNotFound_ForAnIdTheCatalogueDoesNotHave()
    {
        var catalog = new Mock<IBadgesCatalogService>();

        var result = await new GetBadgeQueryHandler(catalog.Object).Handle(new GetBadgeQuery("nope"), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("BadgeNotFound", result.ErrorCode);
    }

    [Fact]
    public async Task GetGroupedProbe_ShouldAnswerNotFound_ForAnIdTheCatalogueDoesNotHave()
    {
        var catalog = new Mock<IProbesCatalogService>();

        var result = await new GetGroupedProbeQueryHandler(catalog.Object).Handle(new GetGroupedProbeQuery("nope"), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("ProbeNotFound", result.ErrorCode);
    }

    [Fact]
    public async Task GetAwardImage_ShouldAnswerNotFound_WhenTheLevelHasNoPicture()
    {
        var images = new Mock<IAwardImagesCatalog>();

        var result = await new GetAwardImageQueryHandler(images.Object).Handle(new GetAwardImageQuery(9, Colored: true), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("AwardImageNotFound", result.ErrorCode);
    }

    [Fact]
    public async Task GetKurinNumberRange_ShouldRunFromOneToTheConfiguredCeiling()
    {
        var handler = new GetKurinNumberRangeQueryHandler(Options.Create(new KurinNumberOptions { MaxNumber = 75 }));

        var result = await handler.Handle(new GetKurinNumberRangeQuery(), CancellationToken.None);

        Assert.Equal(KurinNumberOptions.MinNumber, result.Data!.Min);
        Assert.Equal(75, result.Data.Max);
    }
}
