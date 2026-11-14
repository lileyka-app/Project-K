namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>The board, one page per column, and the targets its filter can offer.</summary>
public sealed record AgendaBoardDto
{
    public required List<AgendaBoardColumnDto> Columns { get; set; }
    public required List<AgendaBoardTargetDto> Targets { get; set; }
}
