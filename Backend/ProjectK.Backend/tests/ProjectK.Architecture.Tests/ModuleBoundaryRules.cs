using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using FluentAssertions;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// A module reaches another module only through <c>Common/Contracts/&lt;Other&gt;/</c>: an
/// <c>I…Directory</c> for reads, an event for writes. Naming another module's handler, service,
/// mapping or error catalogue is the shortcut that makes two modules one, so no type in
/// <c>BusinessLogic.Modules.&lt;A&gt;</c> may depend on a type in <c>BusinessLogic.Modules.&lt;B&gt;</c>.
/// <para>
/// Read twice. From IL, like <see cref="MemberBoundaryRules"/>, and for the same reason asserted as a
/// subset: a Release build may optimise a reference away that a Debug build keeps. An entry is the
/// top-level type that crosses and the module it reaches; one that has been fixed is struck by hand.
/// And from source, because IL does not see a <c>const</c>: another module's error code is inlined
/// into the caller and leaves no reference behind. That list is held exactly.
/// </para>
/// </summary>
public class ModuleBoundaryRules
{
    private const string Boundaries = "CONTRIBUTING.md › Бекенд › «Межі модулів»";

    private static readonly Regex ModuleNamespace = new(
        @"^ProjectK\.BusinessLogic\.Modules\.(?<module>\w+)(\.|$)", RegexOptions.CultureInvariant);

    private static readonly Regex ModuleFile = new(
        @"^ProjectK\.BusinessLogic/Modules/(?<module>\w+)/", RegexOptions.CultureInvariant);

    private static readonly Regex NamedModule = new(
        @"\bProjectK\.BusinessLogic\.Modules\.(?<module>\w+)\b", RegexOptions.CultureInvariant);

    /// <summary>The files that spell out another module's namespace, by the module they name.</summary>
    private static readonly string[] NamesAnotherModule =
    [
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Dues/GetMyDuesQuery.cs -> DuesModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Duties/GetMyDutiesQuery.cs -> ScoreModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Growth/GetMyGrowthQuery.cs -> ProbesAndBadgesModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Invitations/AnswerMyInvitationCommand.cs -> KurinModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Invitations/GetMyInvitationsQuery.cs -> KurinModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Life/ExportMyLifePdfQuery.cs -> MemberModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Life/ExportMyLifePdfQuery.cs -> ProbesAndBadgesModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Features/Score/GetMyScoreQuery.cs -> ScoreModule",
        "ProjectK.BusinessLogic/Modules/MeModule/Services/MemberCards.cs -> ProbesAndBadgesModule",
    ];

    /// <summary>
    /// The crossings that were there when the rule was written, one by one. The list only gets
    /// shorter: a new line here is a boundary broken on purpose, and needs a task in the tracker.
    /// </summary>
    private static readonly string[] ReachesIntoAnotherModule =
    [
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Dues.GetMyDuesQueryHandler -> DuesModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Duties.GetMyDutiesQueryHandler -> ScoreModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Growth.GetMyGrowthQueryHandler -> ProbesAndBadgesModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Invitations.AnswerMyInvitationCommandHandler -> KurinModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Invitations.GetMyInvitationsQueryHandler -> KurinModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Life.ExportMyLifePdfQueryHandler -> MemberModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Life.ExportMyLifePdfQueryHandler -> ProbesAndBadgesModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Features.Score.GetMyScoreQueryHandler -> ScoreModule",
        "ProjectK.BusinessLogic.Modules.MeModule.Services.MemberCards -> ProbesAndBadgesModule",
    ];

    [Fact]
    public void Modules_ShouldNotReachIntoEachOther()
    {
        var crossings = ProjectKArchitecture.Loaded.Types
            .Select(type => (type, from: ModuleOf(type)))
            .Where(pair => pair.from is not null)
            .SelectMany(pair => pair.type.Dependencies
                .Select(dependency => ModuleOf(dependency.Target))
                .Where(to => to is not null && to != pair.from)
                .Select(to => $"{TopLevel(pair.type.FullName)} -> {to}"))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var added = crossings.Except(ReachesIntoAnotherModule, StringComparer.Ordinal).ToList();

        added.Should().BeEmpty(
            "a module asks another through Common/Contracts — a Directory to read, an event to write — "
            + "and never names its handlers, services or errors; update the baseline only to shorten it:"
            + Environment.NewLine + string.Join(Environment.NewLine, added));

        var named = SourceTree.Files("ProjectK.BusinessLogic/Modules")
            .SelectMany(file =>
            {
                var from = ModuleFile.Match(file).Groups["module"].Value;
                return NamedModule.Matches(SourceTree.Read(file))
                    .Select(match => match.Groups["module"].Value)
                    .Where(to => to != from)
                    .Select(to => $"{file} -> {to}");
            });

        Baseline.Hold(
            named,
            NamesAnotherModule,
            $"Files that name another module's namespace; ask it through Common/Contracts instead, and give the module its own error codes ({Boundaries}):");
    }

    private static readonly Regex EntityNamespace = new(
        @"^ProjectK\.Common\.Entities\.(?<module>\w+)(\.|$)", RegexOptions.CultureInvariant);

    /// <summary>
    /// The navigations across a module boundary still standing, one by one, with the module they reach.
    /// The list only gets shorter.
    /// </summary>
    private static readonly string[] NavigatesIntoAnotherModule = [];

    /// <summary>
    /// An entity holds another module's entity by key, never by navigation: the navigation is a hidden
    /// JOIN that does not survive splitting the modules, and it ties deleting one to the other. The
    /// relationship itself stays in the model as <c>HasOne&lt;T&gt;()</c> with the same key and delete
    /// rule, so the database does not change. Read by reflection, so the list is held exactly.
    /// </summary>
    [Fact]
    public void Entities_ShouldNotNavigateIntoAnotherModule()
    {
        var offenders = typeof(ProjectK.Common.Entities.MemberModule.Member).Assembly.GetTypes()
            .Select(type => (type, from: EntityModuleOf(type)))
            .Where(pair => pair.from is not null && pair.type.IsClass)
            .SelectMany(pair => pair.type
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(property => (property, to: EntityModuleOf(NavigatedType(property.PropertyType))))
                .Where(nav => nav.to is not null && nav.to != pair.from && nav.to != "Shared")
                .Select(nav => $"{pair.type.FullName}.{nav.property.Name} -> {nav.to}"));

        Baseline.Hold(
            offenders,
            NavigatesIntoAnotherModule,
            $"Entities that navigate into another module's entity; hold its key and configure HasOne<T>() with the same key and delete rule ({Boundaries}):");
    }

    private static Type NavigatedType(Type type) =>
        type.IsGenericType && type.GetGenericArguments() is [var element] && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            ? element
            : type;

    private static string? EntityModuleOf(Type type)
    {
        var match = EntityNamespace.Match(type.Namespace ?? "");
        return match.Success && type.IsClass ? match.Groups["module"].Value : null;
    }

    private static string? ModuleOf(IType type)
    {
        var match = ModuleNamespace.Match(type.Namespace?.FullName ?? "");
        return match.Success ? match.Groups["module"].Value : null;
    }

    private static string TopLevel(string fullName)
    {
        var cut = fullName.IndexOfAny(['/', '+']);
        return cut < 0 ? fullName : fullName[..cut];
    }
}
