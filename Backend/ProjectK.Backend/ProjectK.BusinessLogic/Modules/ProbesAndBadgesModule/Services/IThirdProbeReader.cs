using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// Builds the «Третя проба» tab for the viewer. Every write answers with it too, so the clients
/// keep one picture of the probe and never stitch partial answers together.
/// </summary>
public interface IThirdProbeReader
{
    Task<ThirdProbeDto> ReadAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default);
}
