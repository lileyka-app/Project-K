using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// Where business logic lies on disk (POL-02…POL-05): <c>Modules/&lt;M&gt;/Features/&lt;Area&gt;/</c>, one
/// request per file with its validator and handler, and around the features only <c>Models/</c>,
/// <c>Services/</c>, <c>Events/</c> and the module's <c>…Errors.cs</c>. The verb folders under an area
/// gave 145 folders for 302 files, and the code drifted into private layouts from there.
/// </summary>
public class LayoutRules
{
    private const string Slices = "CONTRIBUTING.md › Бекенд › «Вертикальні зрізи»";

    private static readonly Regex FeatureArea = new(
        @"^ProjectK\.BusinessLogic\.Modules\.(?<module>\w+)\.Features\.(?<area>\w+)$", RegexOptions.CultureInvariant);

    private static readonly Regex ModuleNamespace = new(
        @"^ProjectK\.BusinessLogic\.Modules\.\w+(\.(?<rest>.+))?$", RegexOptions.CultureInvariant);

    private static readonly string[] ModuleFolders = ["Features", "Models", "Services", "Events"];

    private static IEnumerable<Type> WrittenTypes() =>
        SourceTree.BusinessLogic.GetTypes().Where(type =>
            !type.Name.Contains('<', StringComparison.Ordinal)
            && !type.IsDefined(typeof(CompilerGeneratedAttribute), false));

    private static IEnumerable<Type> Requests() =>
        WrittenTypes().Where(type => !type.IsAbstract && !type.IsInterface && typeof(IBaseRequest).IsAssignableFrom(type));

    private static Type? Generic(Type type, Type openGeneric) =>
        type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    [Fact]
    public void EveryRequest_ShouldLiveDirectlyInAFeatureArea()
    {
        var offenders = Requests()
            .Where(request => request.IsNested || !FeatureArea.IsMatch(request.Namespace ?? ""))
            .Select(request => $"{request.FullName} — put it in Modules.<M>.Features.<Area>, one noun after Features, no verb or Commands/Queries folder");

        Baseline.None(offenders, $"Requests outside a feature area ({Slices}):");
    }

    [Fact]
    public void HandlersAndValidators_ShouldSitBesideTheirRequest()
    {
        var offenders = WrittenTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .Select(type => (type, request:
                Generic(type, typeof(IRequestHandler<,>))?.GetGenericArguments()[0]
                ?? Generic(type, typeof(IRequestHandler<>))?.GetGenericArguments()[0]
                ?? Generic(type, typeof(IValidator<>))?.GetGenericArguments()[0]))
            .Where(pair => pair.request is not null
                && typeof(IBaseRequest).IsAssignableFrom(pair.request)
                && pair.type.Namespace != pair.request.Namespace)
            .Select(pair => $"{pair.type.FullName} serves {pair.request!.FullName} — move it into {pair.request.Namespace}, the request's own file");

        Baseline.None(offenders, $"Handlers or validators away from their request ({Slices}):");
    }

    [Fact]
    public void EveryRequest_ShouldHaveTheFileOfItsName()
    {
        var offenders = new List<string>();
        foreach (var request in Requests())
        {
            var match = FeatureArea.Match(request.Namespace ?? "");
            if (!match.Success || request.IsNested)
            {
                continue;
            }

            var name = SourceTree.SourceName(request);
            var file = $"ProjectK.BusinessLogic/Modules/{match.Groups["module"].Value}/Features/{match.Groups["area"].Value}/{name}.cs";
            var declared = SourceTree.Exists(file)
                && Regex.IsMatch(SourceTree.Read(file), $@"\b(class|record|struct)\s+{Regex.Escape(name)}\b");

            if (!declared)
            {
                offenders.Add($"{request.FullName} — declare it, its validator and its handler in {file}");
            }
        }

        Baseline.None(offenders, $"Requests that do not have a file of their own name; one request, one file ({Slices}):");
    }

    [Fact]
    public void Modules_ShouldHoldOnlyFeaturesModelsServicesEventsAndErrors()
    {
        var offenders = new List<string>();

        foreach (var module in SourceTree.Folders("ProjectK.BusinessLogic/Modules"))
        {
            foreach (var folder in SourceTree.Folders(module))
            {
                var name = Path.GetFileName(folder);
                if (!ModuleFolders.Contains(name, StringComparer.Ordinal))
                {
                    offenders.Add($"{folder}/ — a module has only Features/, Models/, Services/ and Events/");
                    continue;
                }

                // Features/<Area>/ is the only level that nests; Models, Services and Events are flat.
                var nested = name == "Features"
                    ? SourceTree.Folders(folder).SelectMany(SourceTree.Folders)
                    : SourceTree.Folders(folder);
                offenders.AddRange(nested.Select(sub => $"{sub}/ — no folders below {(name == "Features" ? "Features/<Area>" : name)}; flatten it"));
            }

            offenders.AddRange(Directory.EnumerateFiles(Path.Combine(SourceTree.BackendRoot, module))
                .Select(Path.GetFileName)
                .Where(file => !Regex.IsMatch(file!, @"^\w+Errors\.cs$"))
                .Select(file => $"{module}/{file} — only <Name>Errors.cs lies at a module's root; the rest goes to Features/, Models/, Services/ or Events/"));
        }

        // The namespaces too: a file can sit in the right folder and still declare itself elsewhere.
        offenders.AddRange(WrittenTypes()
            .Where(type => !type.IsNested)
            .Select(type => (type, match: ModuleNamespace.Match(type.Namespace ?? "")))
            .Where(pair => pair.match.Success && !IsModuleNamespace(pair.type, pair.match.Groups["rest"].Value))
            .Select(pair => $"{pair.type.FullName} — namespace must be Modules.<M>.Features.<Area>, .Models, .Services or .Events"));

        Baseline.None(offenders, $"Module layout off the agreed folders ({Slices}):");
    }

    private static bool IsModuleNamespace(Type type, string rest) =>
        rest switch
        {
            "" => type.Name.EndsWith("Errors", StringComparison.Ordinal),
            "Models" or "Services" or "Events" => true,
            _ => Regex.IsMatch(rest, @"^Features\.\w+$"),
        };

    [Fact]
    public void EveryEventHandler_ShouldLiveInItsModuleEvents()
    {
        var offenders = SourceTree.All
            .SelectMany(SourceTree.TopLevelTypes)
            .Where(type => !type.IsAbstract
                && (type.Name.EndsWith("EventHandler", StringComparison.Ordinal) || Generic(type, typeof(INotificationHandler<>)) is not null))
            .Where(type => !Regex.IsMatch(type.Namespace ?? "", @"^ProjectK\.BusinessLogic\.Modules\.\w+\.Events$"))
            .Select(type => $"{type.FullName} — move it to Modules/<M>/Events/ of the module whose data it changes");

        Baseline.None(offenders, $"Event handlers outside a module's Events folder ({Slices}):");
    }
}
