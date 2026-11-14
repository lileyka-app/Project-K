using System.Text.RegularExpressions;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// One way to map: explicit <c>ToDto</c> / <c>ApplyTo</c> code (POL-03, D-2). AutoMapper was taken out
/// together with its packages; a profile copied a new request field into the database without anyone
/// writing that line, which is exactly what <c>ApplyTo</c> exists to prevent.
/// </summary>
public class MappingRules
{
    private const string Mapping = "CONTRIBUTING.md › Бекенд › «Мапінг»";

    [Fact]
    public void NoAssembly_ShouldReferenceAutoMapper()
    {
        var offenders = SourceTree.All
            .SelectMany(assembly => assembly.GetReferencedAssemblies()
                .Where(reference => reference.Name?.StartsWith("AutoMapper", StringComparison.OrdinalIgnoreCase) == true)
                .Select(reference => $"{assembly.GetName().Name} references {reference.Name}"));

        // A package reference that nothing calls yet leaves no trace in the IL, so the projects are read too.
        var projects = Directory.EnumerateFiles(SourceTree.BackendRoot, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(SourceTree.BackendRoot, "*.props", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"<PackageReference\s+Include=""AutoMapper", RegexOptions.IgnoreCase))
            .Select(file => $"{Path.GetRelativePath(SourceTree.BackendRoot, file).Replace('\\', '/')} references the AutoMapper package");

        Baseline.None(
            offenders.Concat(projects),
            $"AutoMapper is back; map with explicit ToDto/ApplyTo in <Area>Mapping.cs next to the requests ({Mapping}):");
    }
}
