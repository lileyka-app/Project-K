using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The four assemblies, read once. ArchUnitNET reads the compiled IL, so a rule sees a dependency
/// even when it only appears inside a method body — which is where порушення меж модулів живуть.
/// </summary>
public static class ProjectKArchitecture
{
    public static readonly ArchUnitNET.Domain.Architecture Loaded = new ArchLoader()
        .LoadAssemblies(
            typeof(ProjectK.Common.Entities.KurinModule.Member).Assembly,
            typeof(ProjectK.Infrastructure.DbContexts.AppDbContext).Assembly,
            typeof(ProjectK.BusinessLogic.DependencyInjection).Assembly,
            typeof(ProjectK.API.ServiceCollectionExtensions).Assembly)
        .Build();

    /// <summary>
    /// The types a rule flags, as sorted full names. Rules whose violations are a known backlog are
    /// asserted against a baseline list rather than against zero, so the debt is visible and cannot grow.
    /// </summary>
    public static string[] Violations(IArchRule rule) =>
        rule.Evaluate(Loaded)
            .Where(result => !result.Passed)
            .Select(result => (result.EvaluatedObject as ICanBeAnalyzed)?.FullName ?? "?")
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
