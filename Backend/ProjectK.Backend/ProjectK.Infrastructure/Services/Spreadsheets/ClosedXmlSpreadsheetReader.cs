using System.Globalization;
using ClosedXML.Excel;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Infrastructure.Services.Spreadsheets;

/// <inheritdoc />
public sealed class ClosedXmlSpreadsheetReader : ISpreadsheetReader
{
    public IReadOnlyList<SheetRow> Read(Stream content, int maxRows)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return [];
        }

        var used = sheet.RangeUsed();
        if (used is null)
        {
            return [];
        }

        var width = used.ColumnCount();
        var rows = new List<SheetRow>();

        foreach (var row in used.Rows())
        {
            if (rows.Count >= maxRows)
            {
                break;
            }

            var cells = new List<string>(width);
            for (var column = 1; column <= width; column++)
            {
                cells.Add(TextOf(row.Cell(column)));
            }

            // A row someone left blank in the middle of the list is not data and not an error.
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new SheetRow(row.WorksheetRow().RowNumber(), cells));
        }

        return rows;
    }

    private static string TextOf(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        // Dates come out ISO. Excel stores them as numbers and shows them in the machine's locale,
        // so the formatted string would say 01/09/2015 on one computer and 09/01/2015 on another —
        // and the importer would have no way to tell which.
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var moment))
        {
            return moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return cell.GetFormattedString().Trim();
    }
}
