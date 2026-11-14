using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.Common.Contracts.InfrastructureModule;

/// <summary>
/// Turns tables into a spreadsheet file. Kept behind an interface because the format is a delivery
/// detail: what a registry contains is decided in the business layer, and how it is written is not.
/// </summary>
public interface ISpreadsheetWriter
{
    /// <summary>
    /// The bytes of a workbook, one sheet per table, each with a bold header row. Several sheets
    /// rather than one because a реєстр is not one list: юнаки, впорядники and чисельність answer
    /// different questions and have different columns, and stacking them on one sheet would make
    /// every one of them unsortable.
    /// </summary>
    byte[] Write(IReadOnlyList<SheetTable> sheets);
}
