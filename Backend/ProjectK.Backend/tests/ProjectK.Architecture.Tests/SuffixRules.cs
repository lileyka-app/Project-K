using System.Reflection;
using System.Text.RegularExpressions;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The suffix says the role (POL-10): one role, one suffix. Every one-off suffix — <c>Provider</c>,
/// <c>Source</c>, <c>Lookups</c>, <c>Resolver</c> — was a reader wondering what the class does.
/// </summary>
public class SuffixRules
{
    private const string Vocabulary = "CONTRIBUTING.md › Бекенд › «Словник імен»";
    private const string Boundaries = "CONTRIBUTING.md › Бекенд › «Межі модулів»";

    private static readonly string[] StaticSuffixes =
        ["Rules", "Names", "Mapping", "Extensions", "Errors", "Trail", "Cookie", "Policies", "Access"];

    private static readonly string[] StaticNames = ["Program", "DependencyInjection"];

    /// <summary>
    /// The roles from the table in CONTRIBUTING, plus <c>Directory</c> for the implementation of an
    /// <c>I…Directory</c> contract and <c>Validator</c> for a FluentValidation descendant (named after
    /// its library base type).
    /// </summary>
    private static readonly string[] ServiceSuffixes =
        ["Service", "Reader", "Writer", "Store", "Access", "Trail", "Directory", "Validator"];

    private static readonly string[] DirectoryVerbs = ["Get", "Find", "Count", "Has", "Exists", "Is"];

    /// <summary>
    /// Seed data, F-11: the static seeders, and the краї list the model seeds from DbContexts.
    /// Nothing else may join it.
    /// </summary>
    private static readonly string[] StaticsWithoutASuffix =
    [
        "ProjectK.Infrastructure.DbContexts.KraiSeed",
        "ProjectK.Infrastructure.Seeding.DataSeeder",
        "ProjectK.Infrastructure.Seeding.E2eFixtureSeeder",
    ];

    /// <summary>
    /// Named in CONTRIBUTING itself: the authorisation engine lives under its own names, the one
    /// "current person" is <c>ICurrentMember</c>, and the journal is written through <c>IKurinAudit</c>.
    /// </summary>
    private static readonly string[] NamedInTheVocabulary =
    [
        "ProjectK.BusinessLogic.Modules.AuditModule.Services.KurinAudit",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.AccessContextResolver",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.AccessSnapshotFactory",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.CurrentPermissions",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.PermissionResolver",
        "ProjectK.BusinessLogic.Modules.MemberModule.Services.CurrentMember",
    ];

    /// <summary>
    /// Classes that are not a role but the concept itself (<c>DuesLedger</c>, <c>ScoreBook</c>). The
    /// list is the state at POL-11; a new class without a role suffix is added here on purpose, and one
    /// that turns out to be a reader or a writer leaves the list when it is renamed.
    /// </summary>
    private static readonly string[] DomainNouns =
    [
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.LoginThrottle",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.Reauthentication",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.RefreshTokenInvalidation",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.ResourceAccessServiceInstrumentationDecorator",
        "ProjectK.BusinessLogic.Modules.AuthModule.Services.SavedSessionList",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesAccountRows",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesAccrual",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesKurinShape",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesLedger",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesNames",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesTagLens",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaRoster",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaTargetNames",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.ArchiveDangerZoneEntry",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.ArchiveReach",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.CampDateProblem",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.GroupProbeEditions",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.KurinTagAssignments",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.KurinTagVisibility",
        "ProjectK.BusinessLogic.Modules.MemberModule.Services.MembershipAskRouting",
        "ProjectK.BusinessLogic.Modules.MemberModule.Services.MembershipMoves",
        "ProjectK.BusinessLogic.Modules.MeModule.Services.DutyRights",
        "ProjectK.BusinessLogic.Modules.MeModule.Services.MeAgendaScopes",
        "ProjectK.BusinessLogic.Modules.MeModule.Services.MemberCards",
        "ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services.CampCertificateHandlerBase",
        "ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services.KurinCatalog",
        "ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services.KurinCatalogView",
        "ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services.MemberProbeLine",
        "ProjectK.BusinessLogic.Modules.ScoreModule.Services.ScoreAttendanceBook",
        "ProjectK.BusinessLogic.Modules.ScoreModule.Services.ScoreBook",
        "ProjectK.BusinessLogic.Modules.ScoreModule.Services.ScoreLedger",
    ];

    /// <summary>
    /// Debt: records that carry values between a service and its callers and belong in the
    /// module's <c>Models/</c>, where they would need no suffix at all.
    /// </summary>
    private static readonly string[] RecordsAwayFromModels =
    [
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesBox",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesKrai",
        "ProjectK.BusinessLogic.Modules.DuesModule.Services.DuesPrice",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaAssignReach",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaNames",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaStake",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.AgendaViewerContext",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.ArchiveReachResult",
        "ProjectK.BusinessLogic.Modules.KurinModule.Services.KurinTagSettingsState",
        "ProjectK.BusinessLogic.Modules.MemberModule.Services.MembershipAskRoute",
        "ProjectK.BusinessLogic.Modules.MeModule.Services.MeKurinContext",
        "ProjectK.BusinessLogic.Modules.ScoreModule.Services.EventOccurrence",
    ];

    /// <summary>A yes/no question without the <c>Is</c> in front; debt, the rename touches the dues callers.</summary>
    private static readonly string[] DirectoryMethodsOffTheVerbs =
    [
        "ProjectK.Common.Contracts.DuesModule.IDuesDirectory.GroupBoxSettledAsync",
    ];

    [Fact]
    public void EveryStaticClass_ShouldEndInARoleSuffix()
    {
        var offenders = SourceTree.All
            .SelectMany(SourceTree.TopLevelTypes)
            .Where(SourceTree.IsStatic)
            .Where(type => !StaticNames.Contains(type.Name, StringComparer.Ordinal)
                && !StaticSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Select(type => type.FullName!);

        Baseline.Hold(
            offenders,
            StaticsWithoutASuffix,
            $"Static classes without a role suffix ({string.Join(", ", StaticSuffixes)}); "
            + $"a decision is …Rules, a label …Names, ToDto/ApplyTo …Mapping, extension methods …Extensions ({Vocabulary}):");
    }

    [Fact]
    public void ModuleServices_ShouldEndInARoleSuffixOrBeADomainNoun()
    {
        var offenders = SourceTree.TopLevelTypes(SourceTree.BusinessLogic)
            .Where(type => type.IsClass && !SourceTree.IsStatic(type)
                && Regex.IsMatch(type.Namespace!, @"^ProjectK\.BusinessLogic\.Modules\.\w+\.Services$"))
            .Where(type => !ServiceSuffixes.Any(suffix => SourceTree.SourceName(type).EndsWith(suffix, StringComparison.Ordinal)))
            .Select(type => type.FullName!);

        Baseline.Hold(
            offenders,
            [.. NamedInTheVocabulary, .. DomainNouns, .. RecordsAwayFromModels],
            $"Module services without a role suffix ({string.Join(", ", ServiceSuffixes)}): "
            + $"rename by behaviour, or add the class to DomainNouns if it models exactly that concept ({Vocabulary}):");
    }

    [Fact]
    public void Directories_ShouldOnlyAnswer()
    {
        var offenders = SourceTree.TopLevelTypes(SourceTree.Common)
            .Where(type => type.IsInterface
                && type.Name.EndsWith("Directory", StringComparison.Ordinal)
                && type.Namespace!.StartsWith("ProjectK.Common.Contracts.", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Where(method => !DirectoryVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal))
                    || !IsAnsweringTask(method.ReturnType))
                .Select(method => $"{type.FullName}.{method.Name}"));

        Baseline.Hold(
            offenders,
            DirectoryMethodsOffTheVerbs,
            $"Directory methods that do more than answer; a directory method starts with {string.Join("/", DirectoryVerbs)} "
            + $"and returns Task<…>, and a change in another module goes through an event ({Boundaries}):");
    }

    private static bool IsAnsweringTask(Type returnType) =>
        returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);
}
