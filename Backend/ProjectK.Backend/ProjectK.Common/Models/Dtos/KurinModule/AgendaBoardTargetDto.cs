using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

public sealed record AgendaBoardTargetDto
{
    public required AgendaTargetType TargetType { get; set; }
    public required Guid TargetKey { get; set; }
    public required string Label { get; set; }
}
