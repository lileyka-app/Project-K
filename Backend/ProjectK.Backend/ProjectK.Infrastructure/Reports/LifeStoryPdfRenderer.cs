using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectK.Infrastructure.Reports;

/// <summary>The life story on A4, in the kurin report's type and colours, one table per section.</summary>
public sealed class LifeStoryPdfRenderer : ILifeStoryPdfRenderer
{
    public byte[] Render(LifeStoryData story, string? timeZoneId = null)
    {
        var zone = ReportClock.Resolve(timeZoneId);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(9.5f).FontFamily(Fonts.Lato));

                page.Header().PaddingBottom(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(column =>
                {
                    column.Item().Text("Пластовий життєпис").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken1);
                    column.Item().Text(story.FullName).FontSize(17).Bold();
                    column.Item().Text($"Сформовано: {ReportClock.Format(story.GeneratedAtUtc, zone)} {ReportClock.Label(zone)}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(14);
                    if (story.Sections.Count == 0)
                    {
                        column.Item().Text("Записів поки немає.").FontColor(Colors.Grey.Darken1);
                    }

                    foreach (var section in story.Sections)
                    {
                        column.Item().Element(container => ComposeSection(container, section));
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Сторінка ");
                    text.CurrentPageNumber();
                    text.Span(" з ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeSection(IContainer container, LifeStorySection section)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item()
                .PaddingVertical(3)
                .BorderBottom(1)
                .BorderColor(Colors.Blue.Lighten2)
                .Text(section.Title)
                .FontSize(12)
                .Bold()
                .FontColor(Colors.Blue.Darken2);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(128);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(4);
                });

                foreach (var row in section.Rows)
                {
                    table.Cell().Element(Cell).Text(row.When).FontColor(Colors.Grey.Darken2);
                    table.Cell().Element(Cell).Text(row.What).SemiBold();
                    table.Cell().Element(Cell).Text(row.Detail ?? string.Empty).FontColor(Colors.Grey.Darken2);
                }
            });
        });
    }

    private static IContainer Cell(IContainer container)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingRight(8);
}
