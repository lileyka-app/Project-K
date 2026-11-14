using ProjectK.Common.Records.KurinModule;
using ProjectK.Infrastructure.Reports;
using QuestPDF.Infrastructure;

namespace ProjectK.API.Tests.Services.Reports;

public sealed class LifeStoryPdfRendererTests
{
    private static string Render(LifeStoryData story)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return System.Text.Encoding.ASCII.GetString(new LifeStoryPdfRenderer().Render(story, "Europe/Kyiv"));
    }

    [Fact]
    public void Render_WithSections_ProducesAPdf()
    {
        var body = Render(new LifeStoryData("Тарас Шевченко", DateTime.UtcNow,
        [
            new LifeStorySection("Членства", [new LifeStoryRow("01.09.2015 – дотепер", "Курінь ч. 47", "гурток Леви")]),
            new LifeStorySection("Вишколи", [new LifeStoryRow("01.07.2024 – 10.07.2024", "12-й КВЗ", "комендант: Ярема")])
        ]));

        Assert.StartsWith("%PDF-1.", body);
    }

    [Fact]
    public void Render_WithNothingWritten_StillProducesAPdf()
    {
        Assert.StartsWith("%PDF-1.", Render(new LifeStoryData("Тарас Шевченко", DateTime.UtcNow, [])));
    }
}
