using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>The kurin's real directories over a test's unit of work, so its Groups, Kurins and KurinTags setups answer.</summary>
internal static class KurinDirectories
{
    public static IKurinDirectory Over(IUnitOfWork unitOfWork) => new KurinDirectory(unitOfWork);

    public static IKurinTagDirectory TagsOver(IUnitOfWork unitOfWork, IKurinTagVisibility? visibility = null)
        => new KurinTagDirectory(visibility ?? Mock.Of<IKurinTagVisibility>(), unitOfWork);
}
