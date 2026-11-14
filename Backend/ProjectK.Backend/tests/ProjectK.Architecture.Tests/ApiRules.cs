using System.Text.RegularExpressions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The host stays a thin shell (POL-06…POL-08): a controller turns HTTP into a MediatR request and a
/// <c>ServiceResult</c> back into HTTP, and nothing else. Every decision a controller made on its own
/// was a decision the handler tests never saw.
/// </summary>
public class ApiRules
{
    private const string WhatGoesWhere = "CONTRIBUTING.md › Бекенд › «Що куди лягає»";
    private const string Errors = "CONTRIBUTING.md › Бекенд › «Відповіді та помилки»";
    private const string Layers = "CONTRIBUTING.md › Бекенд › «Шари»";

    /// <summary>
    /// The e2e fixture endpoint resets the database itself and reads configuration and the
    /// environment to guard that; it has no handler to delegate to (D-02 in the extraction backlog).
    /// </summary>
    private static readonly string[] InjectsMoreThanTheMediator =
    [
        "ProjectK.API.Controllers.TestModule.E2ETestController",
    ];

    /// <summary>The same e2e endpoint answers its own 503 with a bare <c>{ message }</c>.</summary>
    private static readonly string[] WritesAnAnonymousError =
    [
        "ProjectK.API/Controllers/TestModule/E2ETestController.cs",
    ];

    /// <summary>
    /// Outside the composition root the host still names two Infrastructure types: the GeoIP lookup
    /// the security middleware calls and the telemetry counters the request middleware bumps. Both
    /// want a contract in Common. The e2e controller is the D-02 case above.
    /// </summary>
    private static readonly string[] ReachesIntoInfrastructure =
    [
        "ProjectK.API.Controllers.TestModule.E2ETestController",
        "ProjectK.API.Middleware.SecurityHardeningMiddleware",
        "ProjectK.API.Middleware.TelemetryCountingMiddleware",
    ];

    private static readonly Regex TypeDeclaration = new(
        @"^[ \t]*(?:(?:public|internal|private|protected|file|sealed|static|abstract|partial|readonly|unsafe|new)\s+)*(?:record\s+(?:class|struct)|class|record|struct|interface|enum)\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex AnonymousError = new(
        @"new\s*\{\s*(?:error|errors|code|message)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IEnumerable<Type> Controllers() =>
        SourceTree.TopLevelTypes(SourceTree.Api).Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    /// <summary>
    /// Not even a logger: no controller logs today, and what is worth logging happens in the handler,
    /// the pipeline behaviours or the middleware.
    /// </summary>
    [Fact]
    public void Controllers_ShouldInjectOnlyTheMediator()
    {
        var offenders = Controllers()
            .Where(controller => controller.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType != typeof(IMediator)))
            .Select(controller => controller.FullName!);

        Baseline.Hold(
            offenders,
            InjectsMoreThanTheMediator,
            $"Controllers that take more than IMediator: move the work into a handler and send it ({WhatGoesWhere}):");
    }

    [Fact]
    public void ControllerFiles_ShouldDeclareOnlyTheController()
    {
        // Nested request bodies and helper records hide here first; their place is Common/Dtos.
        var strayTypes = SourceTree.Api.GetTypes()
            .Where(type => type.Namespace?.StartsWith("ProjectK.API.Controllers", StringComparison.Ordinal) == true
                && !type.Name.Contains('<', StringComparison.Ordinal)
                && !typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => $"{type.FullName} — move it to Common/Dtos/<Module>/ (or API/Models/Requests/ when it carries an IFormFile)");

        var strayFiles = SourceTree.Files("ProjectK.API/Controllers")
            .Select(file => (file, names: TypeDeclaration.Matches(SourceTree.Read(file)).Select(m => m.Groups["name"].Value).ToList()))
            .Where(pair => pair.names.Count != 1 || pair.names[0] != Path.GetFileNameWithoutExtension(pair.file))
            .Select(pair => $"{pair.file} declares [{string.Join(", ", pair.names)}] — a controller file holds the controller of its own name and nothing else");

        Baseline.None(strayTypes.Concat(strayFiles), $"Controller files with types besides the controller ({WhatGoesWhere}):");
    }

    [Fact]
    public void Controllers_ShouldNotAnswerWithAnonymousErrors()
    {
        var offenders = SourceTree.Files("ProjectK.API/Controllers")
            .Where(file => AnonymousError.IsMatch(SourceTree.Read(file)));

        Baseline.Hold(
            offenders,
            WritesAnAnonymousError,
            $"Controllers that build an error body by hand; return ServiceResult.Failure through ToActionResult or this.Failure(...) ({Errors}):");
    }

    [Fact]
    public void Api_ShouldReachInfrastructureOnlyFromTheCompositionRoot()
    {
        // Program, the DI entry point and Hosting/ wire Infrastructure up; that is what they are for.
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.API(\..*)?")
            .And().DoNotHaveFullName("ProjectK.API.Program")
            .And().DoNotHaveFullName("ProjectK.API.ServiceCollectionExtensions")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.API\.Hosting(\..*)?")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"ProjectK\.Infrastructure(\..*)?");

        ProjectKArchitecture.Violations(rule).Should().BeSubsetOf(
            ReachesIntoInfrastructure,
            $"the host talks to Infrastructure through Common contracts outside Program, ServiceCollectionExtensions and Hosting/ ({Layers}); "
            + "a new entry here is a new crossing — declare an interface in Common/Contracts instead");
    }
}
