using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectK.Infrastructure;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// Repositories are reached through <c>IUnitOfWork</c> only. A repository registered on its own gets
/// its own instance next to the one the unit of work hands out, and a handler that holds both saves
/// one and silently drops the other.
/// </summary>
public class RepositoryRules
{
    private const string Repositories = "CONTRIBUTING.md › Бекенд › «Репозиторії»";

    /// <summary>
    /// Both sides of every switch in <c>AddInfrastructure</c>: a fresh self-host with nothing configured,
    /// and a host with mail, feedback, Web Push and telemetry all on. Only registrations are read; no
    /// service is resolved, so nothing connects anywhere.
    /// </summary>
    public static TheoryData<string> Hosts => ["bare", "everything on"];

    private static IConfiguration Configuration(string host) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(host == "bare"
                ? []
                : new Dictionary<string, string?>
                {
                    ["Email:Provider"] = "Resend",
                    ["Email:ApiKey"] = "architecture-test",
                    ["Feedback:GitHub:Token"] = "architecture-test",
                    ["WebPush:PublicKey"] = "architecture-test",
                    ["WebPush:PrivateKey"] = "architecture-test",
                    ["Telemetry:Enabled"] = "true",
                })
            .Build();

    [Theory]
    [MemberData(nameof(Hosts))]
    public void AddInfrastructure_ShouldNotRegisterARepositoryOnItsOwn(string host)
    {
        var services = new ServiceCollection().AddInfrastructure(Configuration(host));

        services.Should().NotBeEmpty();
        var offenders = services
            .SelectMany(descriptor => new[] { descriptor.ServiceType, descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType })
            .Where(type => type is not null && SourceTree.SourceName(type).EndsWith("Repository", StringComparison.Ordinal))
            .Select(type => $"{type!.FullName} — reach it through IUnitOfWork (or the module's own unit of work) instead");

        Baseline.None(offenders, $"Repositories registered directly in DI with the {host} configuration ({Repositories}):");
    }
}
