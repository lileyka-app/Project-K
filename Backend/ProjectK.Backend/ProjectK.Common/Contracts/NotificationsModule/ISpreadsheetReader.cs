using ProjectK.Common.Records.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

/// <summary>
/// Reads a spreadsheet back into rows of text. Everything arrives as a string on purpose: a roster
/// is written by people, and the same column can hold a date in one row and a note in the next —
/// deciding what a value means is the importer's job, not the reader's.
/// </summary>
public interface ISpreadsheetReader
{
    /// <summary>
    /// The first sheet, up to <paramref name="maxRows"/> rows. A cell Excel considers a date comes
    /// back as <c>yyyy-MM-dd</c> so it cannot be misread as day-first or month-first.
    /// </summary>
    IReadOnlyList<SheetRow> Read(Stream content, int maxRows);
}
