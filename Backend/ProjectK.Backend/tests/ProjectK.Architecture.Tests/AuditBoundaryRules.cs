using FluentAssertions;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ProjectK.Architecture.Tests;

/// <summary>
/// The kurin's journal is written in one way and read in one way. A handler records through
/// <c>IKurinAudit</c>, which stamps the actor and strips private data; reaching the writer or the
/// table directly would skip both. Empty, and it stays empty.
/// </summary>
public class AuditBoundaryRules
{
    [Fact]
    public void JournalData_ShouldBeReachedOnlyFromTheAuditModule()
    {
        var rule = Types()
            .That().ResideInNamespaceMatching(@"ProjectK\.(BusinessLogic|API)\..*")
            .And().DoNotResideInNamespaceMatching(@"ProjectK\.BusinessLogic\.Modules\.AuditModule(\..*)?")
            .Should().NotDependOnAny(
                typeof(KurinAuditEvent),
                typeof(IKurinAuditWriter),
                typeof(IKurinJournalReader));

        ProjectKArchitecture.Violations(rule).Should().BeEmpty(
            "every journal entry goes through IKurinAudit, and the journal is read through the audit module's query");
    }
}
