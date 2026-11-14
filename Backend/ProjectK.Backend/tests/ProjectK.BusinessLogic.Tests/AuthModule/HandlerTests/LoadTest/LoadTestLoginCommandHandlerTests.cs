using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.LoadTest;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.LoadTest;

/// <summary>
/// The handler mints a token for the seeded load-test account, so the only thing between it and
/// an anonymous caller is the configured key. Empty means off, which is how it ships:
/// appsettings.json leaves LoadTestLoginKey blank. Neither case reaches the account, so the
/// handler is built without one.
/// </summary>
public sealed class LoadTestLoginCommandHandlerTests
{
    private static LoadTestLoginCommandHandler HandlerWithKey(string? configuredKey) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTestLoginKey"] = configuredKey })
                .Build(),
            userManager: null!,
            jwtService: null!,
            access: null!);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Handle_ShouldRefuse_WhenNoKeyIsConfigured(string? configuredKey)
    {
        var result = await HandlerWithKey(configuredKey).Handle(new LoadTestLoginCommand("anything"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Unauthorized);
        result.ErrorCode.Should().Be("InvalidApiKey");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheKeyDoesNotMatch()
    {
        var result = await HandlerWithKey("the-real-key").Handle(new LoadTestLoginCommand("not-the-real-key"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Unauthorized);
        result.ErrorCode.Should().Be("InvalidApiKey");
    }
}
