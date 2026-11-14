using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>The one place a third-probe file comes into being: on whichever write needs it first.</summary>
internal static class ThirdProbeFileWriter
{
    public static async Task<ThirdProbeFile> GetOrAddAsync(IUnitOfWork unitOfWork, Guid memberKey, string probeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken) is { } file)
        {
            return file;
        }

        file = new ThirdProbeFile { MemberKey = memberKey, ProbeId = probeId };
        unitOfWork.ProbeProjects.AddFile(file);
        return file;
    }
}
