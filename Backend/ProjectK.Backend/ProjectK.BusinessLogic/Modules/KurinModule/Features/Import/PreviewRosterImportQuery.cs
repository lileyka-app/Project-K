using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.NotificationsModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>Reads an uploaded roster and says what it looks like. Writes nothing.</summary>
public sealed record PreviewRosterImportQuery(Stream Content) : IRequest<ServiceResult<RosterPreview>>;

/// <summary>One column of the file: its heading, an example of what is in it, and our guess.</summary>
public sealed record RosterColumnPreview(
    int Index,
    string Header,
    string? Sample,
    RosterField SuggestedField,
    PlastLevel? SuggestedLevel);

/// <summary>
/// The file as the mapping screen needs it: the columns to map, and every data row, so the
/// import can be planned and applied without uploading the file a second time.
/// </summary>
public sealed record RosterPreview(
    IReadOnlyList<RosterColumnPreview> Columns,
    IReadOnlyList<SheetRow> Rows);

public sealed class PreviewRosterImportQueryHandler
    : IRequestHandler<PreviewRosterImportQuery, ServiceResult<RosterPreview>>
{
    /// <summary>
    /// A kurin is dozens of people, not thousands. The cap is here so a wrong file cannot take
    /// the process down, not because anyone would legitimately reach it.
    /// </summary>
    private const int MaxRows = 2_000;

    private readonly ISpreadsheetReader _reader;

    public PreviewRosterImportQueryHandler(ISpreadsheetReader reader)
    {
        _reader = reader;
    }

    public Task<ServiceResult<RosterPreview>> Handle(
        PreviewRosterImportQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SheetRow> rows;
        try
        {
            rows = _reader.Read(request.Content, MaxRows + 1);
        }
        catch (Exception)
        {
            // Anything the spreadsheet library refuses is the same answer to the person holding
            // the file: this is not a workbook we can read.
            return Task.FromResult(ServiceResult<RosterPreview>.Failure(
                ResultType.BadRequest, KurinErrors.UnreadableFile, "This file could not be read as a spreadsheet."));
        }

        if (rows.Count == 0)
        {
            return Task.FromResult(ServiceResult<RosterPreview>.Failure(
                ResultType.BadRequest, KurinErrors.EmptyFile, "The spreadsheet has no rows."));
        }

        var header = rows[0];
        var body = rows.Skip(1).ToList();

        var columns = header.Cells
            .Select((text, index) =>
            {
                var (field, level) = RosterHeaderRules.Guess(text);
                return new RosterColumnPreview(
                    index,
                    text,
                    FirstValueIn(body, index),
                    field,
                    level);
            })
            .ToList();

        return Task.FromResult(new ServiceResult<RosterPreview>(
            ResultType.Success,
            new RosterPreview(columns, body)));
    }

    /// <summary>
    /// The first thing actually written in a column, shown next to its heading. A heading alone
    /// is often not enough to tell a date of birth from a date of заприсяження; one real value is.
    /// </summary>
    private static string? FirstValueIn(IReadOnlyList<SheetRow> rows, int index)
        => rows
            .Select(row => index < row.Cells.Count ? row.Cells[index] : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
