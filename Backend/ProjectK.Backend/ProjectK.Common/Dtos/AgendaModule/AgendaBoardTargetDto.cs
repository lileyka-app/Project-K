using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.AgendaModule;

public sealed record AgendaBoardTargetDto
{
    public required AgendaTargetType TargetType { get; set; }
    public required Guid TargetKey { get; set; }
    public required string Label { get; set; }
}
