using Moq;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>Units of work that hand out a test's own repository, for services that reach it only through one.</summary>
internal static class UnitOfWorks
{
    /// <summary>
    /// A unit of work whose <see cref="IUnitOfWork.ResourceScopes"/> is <paramref name="scopes"/>; set on
    /// <paramref name="unitOfWork"/> when the test already holds one.
    /// </summary>
    public static IUnitOfWork WithScopes(IResourceScopeRepository scopes, Mock<IUnitOfWork>? unitOfWork = null)
    {
        unitOfWork ??= new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.ResourceScopes).Returns(scopes);
        return unitOfWork.Object;
    }

    /// <summary>The same for <see cref="IUnitOfWork.KurinReports"/>.</summary>
    public static IUnitOfWork WithReports(IKurinReportRepository reports, Mock<IUnitOfWork> unitOfWork)
    {
        unitOfWork.SetupGet(x => x.KurinReports).Returns(reports);
        return unitOfWork.Object;
    }

    /// <summary>The journal's unit of work, reading back <paramref name="journal"/>.</summary>
    public static IAuditUnitOfWork WithJournal(IKurinJournalRepository journal)
    {
        var audit = new Mock<IAuditUnitOfWork>();
        audit.SetupGet(x => x.KurinJournal).Returns(journal);
        return audit.Object;
    }
}
