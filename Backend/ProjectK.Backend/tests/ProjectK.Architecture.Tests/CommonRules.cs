using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The shape of <c>ProjectK.Common</c> (POL-09): kind first, then module — <c>Common/&lt;Kind&gt;/&lt;Module&gt;/</c> —
/// and eight kinds, no ninth. It is the project every other one reads, so a type in the wrong drawer
/// there is a type everyone looks for in the wrong place.
/// </summary>
public class CommonRules
{
    private const string CommonLayout = "CONTRIBUTING.md › Бекенд › «Схема тек Common»";
    private const string WhatGoesWhere = "CONTRIBUTING.md › Бекенд › «Що куди лягає»";
    private const string DtosAndRecords = "CONTRIBUTING.md › Бекенд › «DTO проти Records»";

    private static readonly string[] Kinds = ["Entities", "Contracts", "Dtos", "Records", "Events", "Enums", "Rules", "Options"];

    /// <summary>
    /// Settings the host binds before anything else is up, read by nobody but the host: the
    /// client-address source the rate limiter partitions on, and the security-patch switch. The
    /// table in CONTRIBUTING still lists <c>…Options</c> under <c>API/Hosting/</c> for them.
    /// </summary>
    private static readonly string[] OptionsOutsideCommon =
    [
        "ProjectK.API.Hosting.ClientIpOptions",
        "ProjectK.API.Hosting.SecurityPatchOptions",
    ];

    private static readonly Regex PublicTopLevelType = new(
        @"^public\s+(?:(?:sealed|static|abstract|partial|readonly|unsafe|new)\s+)*(?:record\s+(?:class|struct)|class|record|struct|interface|enum|delegate\s+[\w<>\[\],? ]+?)\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static IEnumerable<Type> CommonTypes() => SourceTree.TopLevelTypes(SourceTree.Common);

    [Fact]
    public void Common_ShouldHaveOnlyTheEightKinds()
    {
        var namespaces = CommonTypes()
            .Where(type => !Kinds.Any(kind => type.Namespace == $"ProjectK.Common.{kind}"
                || type.Namespace!.StartsWith($"ProjectK.Common.{kind}.", StringComparison.Ordinal)))
            .Select(type => $"{type.FullName} — namespace must start with ProjectK.Common.<Kind>, Kind one of {string.Join(", ", Kinds)}");

        var folders = SourceTree.Folders("ProjectK.Common")
            .Where(folder => !Kinds.Contains(Path.GetFileName(folder), StringComparer.Ordinal))
            .Select(folder => $"{folder}/ — Common has only the folders {string.Join(", ", Kinds)}");

        var looseFiles = Directory.EnumerateFiles(Path.Combine(SourceTree.BackendRoot, "ProjectK.Common"), "*.cs")
            .Select(Path.GetFileName)
            .Where(file => file != "GlobalSuppressions.cs")
            .Select(file => $"ProjectK.Common/{file} — put it under Common/<Kind>/<Module>/");

        Baseline.None(namespaces.Concat(folders).Concat(looseFiles), $"Common outside its eight kinds ({CommonLayout}):");
    }

    [Fact]
    public void CommonFiles_ShouldHoldOnePublicTypeNamedLikeTheFile()
    {
        var offenders = SourceTree.Files("ProjectK.Common")
            .Select(file => (file, names: PublicTopLevelType.Matches(SourceTree.Read(file)).Select(m => m.Groups["name"].Value).ToList()))
            .Where(pair => pair.names.Count > 1
                || (pair.names.Count == 1 && pair.names[0] != Path.GetFileNameWithoutExtension(pair.file)))
            .Select(pair => $"{pair.file} declares [{string.Join(", ", pair.names)}] — one public type per file, in the file of its name; a companion record or enum goes to its own kind");

        Baseline.None(offenders, $"Common files with more than one public type ({CommonLayout}):");
    }

    [Fact]
    public void EveryOptionsType_ShouldLiveInCommonOptions()
    {
        // IConfigureOptions<T> implementations are named after what they configure, not settings themselves.
        var offenders = SourceTree.All
            .SelectMany(SourceTree.TopLevelTypes)
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal)
                && !type.GetInterfaces().Any(i => i.IsGenericType
                    && (i.GetGenericTypeDefinition() == typeof(IConfigureOptions<>)
                        || i.GetGenericTypeDefinition() == typeof(IPostConfigureOptions<>))))
            .Where(type => !type.Namespace!.StartsWith("ProjectK.Common.Options.", StringComparison.Ordinal))
            .Select(type => type.FullName!);

        Baseline.Hold(
            offenders,
            OptionsOutsideCommon,
            $"Options types outside Common/Options/<Module>/, whichever layer reads them ({WhatGoesWhere}):");
    }

    [Fact]
    public void Enums_ShouldHoldNoStaticClasses()
    {
        var offenders = CommonTypes()
            .Where(type => type.Namespace!.StartsWith("ProjectK.Common.Enums", StringComparison.Ordinal) && SourceTree.IsStatic(type))
            .Select(type => $"{type.FullName} — a static class describing an enum is a …Rules or …Names in Common/Rules/<Module>/");

        Baseline.None(offenders, $"Static classes among the enums ({CommonLayout}):");
    }

    [Fact]
    public void DtosAndRequests_ShouldLiveInCommonDtos()
    {
        var offenders = SourceTree.All
            .SelectMany(SourceTree.TopLevelTypes)
            .Where(type => !type.IsInterface
                && (type.Name.EndsWith("Dto", StringComparison.Ordinal) || type.Name.EndsWith("Request", StringComparison.Ordinal)))
            .Where(type => !type.Namespace!.StartsWith("ProjectK.Common.Dtos.", StringComparison.Ordinal)
                && type.Namespace != "ProjectK.API.Models.Requests")
            .Select(type => type.FullName!);

        Baseline.Hold(
            offenders,
            [.. RequestsThatAreDomainNouns, .. DtosAndRequestsAwayFromDtos],
            $"Dto and Request types away from Common/Dtos; what crosses the API is a …Dto or …Request in Common/Dtos/<Module>/ "
            + $"(API/Models/Requests/ only when it carries an IFormFile), and nothing else takes those suffixes ({DtosAndRecords}):");
    }

    /// <summary>
    /// Tables whose subject is a request someone made — to join a kurin, to open a branch. The word
    /// is the domain's, not the API's, so they stay; a new one is added here deliberately.
    /// </summary>
    private static readonly string[] RequestsThatAreDomainNouns =
    [
        "ProjectK.Common.Entities.KurinModule.KurinBranchRequest",
        "ProjectK.Common.Entities.KurinModule.MembershipRequest",
    ];

    /// <summary>
    /// Debt. The calendar-feed answer is returned to the client from the MeModule's Models and
    /// belongs in Common/Dtos/MeModule; the provisioning value never leaves the server and wants a
    /// name without the API's suffix.
    /// </summary>
    private static readonly string[] DtosAndRequestsAwayFromDtos =
    [
        "ProjectK.BusinessLogic.Modules.MeModule.Models.IssuedCalendarFeedDto",
        "ProjectK.Common.Records.AuthModule.AccountProvisioningRequest",
    ];
}
