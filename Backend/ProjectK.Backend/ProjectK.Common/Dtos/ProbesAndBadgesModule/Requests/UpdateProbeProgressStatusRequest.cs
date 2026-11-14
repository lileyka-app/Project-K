using ProjectK.Common.Enums.ProbesAndBadgesModule;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

public class UpdateProbeProgressStatusRequest
{
    public ProbeProgressStatus Status { get; set; }
    public string? Note { get; set; }
}
