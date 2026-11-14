using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>The one place a third-probe file comes into being: on whichever write needs it first.</summary>
internal static class ThirdProbeFiles
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
