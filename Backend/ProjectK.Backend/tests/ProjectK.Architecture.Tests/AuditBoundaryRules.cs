using FluentAssertions;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The kurin's journal is written in one way and read in few. A handler records through
/// <c>IKurinAudit</c>, which stamps the actor and strips private data; reaching the writer or the
/// table directly would skip both. Reading is the audit module's query, and the проби and вмілості,
/// whose status history the journal holds — through <see cref="IKurinJournalReader"/> only.
/// Empty, and it stays empty.
/// </summary>
public class AuditBoundaryRules
{
    private const string AuditModule = @"ProjectK\.BusinessLogic\.Modules\.AuditModule(\..*)?";
    private const string ProbesAndBadgesModule = @"ProjectK\.BusinessLogic\.Modules\.ProbesAndBadgesModule(\..*)?";

    [Fact]
    public void JournalWriter_ShouldBeReachedOnlyFromTheAuditModule()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.(BusinessLogic|API)\..*")
            .And().DoNotResideInNamespaceMatching(AuditModule)
            .Should().NotDependOnAny(typeof(IKurinAuditWriter));

        ProjectKArchitecture.Violations(rule).Should().BeEmpty(
            "every journal entry goes through IKurinAudit");
    }

    [Fact]
    public void JournalData_ShouldBeReadOnlyByTheAuditModuleAndTheProgressTrails()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.(BusinessLogic|API)\..*")
            .And().DoNotResideInNamespaceMatching(AuditModule)
            .And().DoNotResideInNamespaceMatching(ProbesAndBadgesModule)
            .Should().NotDependOnAny(
                typeof(KurinAuditEvent),
                typeof(IKurinJournalReader));

        ProjectKArchitecture.Violations(rule).Should().BeEmpty(
            "the journal is read through the audit module's query, or by the проби and вмілості for their own trail");
    }
}
