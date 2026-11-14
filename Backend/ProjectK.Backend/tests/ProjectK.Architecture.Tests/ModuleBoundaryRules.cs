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
/// Read from IL, like <see cref="MemberBoundaryRules"/>, and for the same reason asserted as a subset:
/// a Release build may optimise a reference away that a Debug build keeps. An entry is the top-level
/// type that crosses and the module it reaches; one that has been fixed is struck by hand.
/// </para>
/// </summary>
public class ModuleBoundaryRules
{
    private static readonly Regex ModuleNamespace = new(
        @"^ProjectK\.BusinessLogic\.Modules\.(?<module>\w+)(\.|$)", RegexOptions.CultureInvariant);

    /// <summary>
    /// The crossings that were there when the rule was written, one by one. The list only gets
    /// shorter: a new line here is a boundary broken on purpose, and needs a task in the tracker.
    /// </summary>
    private static readonly string[] ReachesIntoAnotherModule =
    [
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
