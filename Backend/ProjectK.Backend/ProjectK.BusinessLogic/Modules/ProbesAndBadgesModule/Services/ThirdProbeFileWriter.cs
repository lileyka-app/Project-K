using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>The one place a third-probe file comes into being: on whichever write needs it first.</summary>
public sealed class ThirdProbeFileWriter
{
    private readonly IUnitOfWork _unitOfWork;

    public ThirdProbeFileWriter(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ThirdProbeFile> GetOrAddAsync(Guid memberKey, string probeId, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.ProbeProjects.GetFileAsync(memberKey, probeId, cancellationToken) is { } file)
        {
            return file;
        }

        file = new ThirdProbeFile { MemberKey = memberKey, ProbeId = probeId };
        _unitOfWork.ProbeProjects.AddFile(file);
        return file;
    }
}
