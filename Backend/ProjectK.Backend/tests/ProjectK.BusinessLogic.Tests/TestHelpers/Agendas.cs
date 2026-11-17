using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>The agenda's real directory over a test's unit of work, so its agenda repository setups answer.</summary>
internal static class Agendas
{
    public static IAgendaDirectory Over(IUnitOfWork unitOfWork, IMembershipDirectory? memberships = null)
        => new AgendaDirectory(unitOfWork, memberships ?? Mock.Of<IMembershipDirectory>(), new OfficeDirectory(unitOfWork), KurinDirectories.TagsOver(unitOfWork));
}
