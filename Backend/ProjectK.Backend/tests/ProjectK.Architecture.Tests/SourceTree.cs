using System.Reflection;
using System.Runtime.CompilerServices;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// What the IL cannot say: which file a type sits in, and what is written in it. Found by walking up
/// from the test binaries to the folder that holds <c>ProjectK.Backend.sln</c>, so the rules work the
/// same from an IDE, from <c>dotnet test</c> and in CI.
/// </summary>
public static class SourceTree
{
    public static readonly string BackendRoot = FindBackendRoot();

    public static readonly Assembly Common = typeof(ProjectK.Common.Entities.MemberModule.Member).Assembly;
    public static readonly Assembly Infrastructure = typeof(ProjectK.Infrastructure.DbContexts.AppDbContext).Assembly;
    public static readonly Assembly BusinessLogic = typeof(ProjectK.BusinessLogic.DependencyInjection).Assembly;
    public static readonly Assembly Api = typeof(ProjectK.API.ServiceCollectionExtensions).Assembly;

    public static readonly Assembly[] All = [Common, Infrastructure, BusinessLogic, Api];

    private static readonly Dictionary<string, string> Contents = new(StringComparer.OrdinalIgnoreCase);

    private static string FindBackendRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ProjectK.Backend.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            "ProjectK.Backend.sln was not found above " + AppContext.BaseDirectory + "; the source rules need the checkout.");
    }

    /// <summary>
    /// Every hand-written <c>.cs</c> file under a folder of the backend, relative to the backend root
    /// with forward slashes. Build output and EF migrations are not code anyone lays out by hand.
    /// </summary>
    public static IReadOnlyList<string> Files(string relativeFolder) =>
        Directory.EnumerateFiles(Path.Combine(BackendRoot, relativeFolder), "*.cs", SearchOption.AllDirectories)
            .Select(Relative)
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal)
                && !path.Contains("/obj/", StringComparison.Ordinal)
                && !path.Contains("/Migrations/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<string> Folders(string relativeFolder) =>
        Directory.EnumerateDirectories(Path.Combine(BackendRoot, relativeFolder))
            .Select(Relative)
            .Where(path => !path.EndsWith("/bin", StringComparison.Ordinal) && !path.EndsWith("/obj", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    public static bool Exists(string relativeFile) => File.Exists(Path.Combine(BackendRoot, relativeFile));

    public static string Read(string relativeFile)
    {
        lock (Contents)
        {
            if (!Contents.TryGetValue(relativeFile, out var text))
            {
                text = File.ReadAllText(Path.Combine(BackendRoot, relativeFile));
                Contents[relativeFile] = text;
            }

            return text;
        }
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(BackendRoot, path).Replace('\\', '/');

    /// <summary>
    /// The types someone wrote, at the top level: nothing nested, nothing the compiler made up for
    /// lambdas, records or regex generators.
    /// </summary>
    public static IEnumerable<Type> TopLevelTypes(Assembly assembly) =>
        assembly.GetTypes().Where(type =>
            !type.IsNested
            && type.Namespace?.StartsWith("ProjectK", StringComparison.Ordinal) == true
            && !type.Name.Contains('<', StringComparison.Ordinal)
            && !type.IsDefined(typeof(CompilerGeneratedAttribute), false));

    /// <summary>The type's name without the generic arity, as it is spelled in source and in a file name.</summary>
    public static string SourceName(Type type)
    {
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    public static bool IsStatic(Type type) => type.IsClass && type.IsAbstract && type.IsSealed;

    public static string Report(IEnumerable<string> offenders) =>
        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders);
}
