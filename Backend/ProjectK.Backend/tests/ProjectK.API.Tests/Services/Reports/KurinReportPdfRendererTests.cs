using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Reports;
using ProjectK.Infrastructure.Reports;
using QuestPDF.Infrastructure;

namespace ProjectK.API.Tests.Services.Reports;

public sealed class KurinReportPdfRendererTests
{
    /// <summary>
    /// The report records are long and positional, and a test that spells every argument out breaks
    /// on each new field whether or not it cares about it. These builders take only what the test
    /// is about.
    /// </summary>
    private static KurinReportMember Member(
        Guid memberKey,
        string fullName = "Test Member",
        Guid? groupKey = null,
        string? groupName = null,
        string? photoBlobName = null,
        byte[]? photo = null,
        PlastLevel? level = null,
        IReadOnlyList<string>? mentoredGroups = null,
        IReadOnlyList<string>? tags = null)
        => new(
            memberKey,
            null,
            groupKey,
            groupName,
            fullName,
            "TM",
            "member@example.com",
            "+380000000000",
            new DateOnly(2010, 1, 1),
            null,
            null,
            photoBlobName,
            photo,
            level,
            mentoredGroups ?? [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            tags ?? []);

    private static KurinReportData Report(
        IReadOnlyList<KurinReportGroup>? groups = null,
        IReadOnlyList<KurinReportMember>? staff = null,
        IReadOnlyList<KurinReportMember>? youth = null,
        IReadOnlyList<KurinReportLevelCount>? tally = null,
        IReadOnlyList<KurinReportMember>? members = null,
        bool showsTags = false)
        => new(
            new KurinReportHeader(new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc), "Manager User", "manager@example.com", "v0.13.0-beta", "Stage8"),
            new KurinReportKurin(Guid.NewGuid(), 1, "Kyiv", "Ukraine", "Patron", "Description"),
            groups ?? [],
            staff ?? [],
            youth ?? [],
            tally ?? [],
            members ?? [],
            showsTags);

    private static string RenderToText(KurinReportData report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return System.Text.Encoding.ASCII.GetString(new KurinReportPdfRenderer().Render(report));
    }

    [Fact]
    public void Render_ShouldReturnPdfBytes()
    {
        var body = RenderToText(Report());

        Assert.StartsWith("%PDF-1.", body);
        Assert.Contains("%%EOF", body);
    }

    /// <summary>
    /// The summary page carries three sections the реєстр also shows. Only that the document renders
    /// is asserted — QuestPDF compresses its text streams, so reading the words back out of the
    /// bytes would test the compressor. What the sections *say* is asserted where it is decided,
    /// in <c>KurinReportDataServiceTests</c>.
    /// </summary>
    [Fact]
    public void Render_ShouldDrawYouthStaffAndTally()
    {
        var body = RenderToText(Report(
            staff: [Member(Guid.NewGuid(), "Виховна Тест", mentoredGroups: ["Gurtok 1", "Gurtok 2"])],
            youth: [Member(Guid.NewGuid(), "Юнак Тест", groupName: "Gurtok 1", level: PlastLevel.Uchasnyk)],
            tally: [new KurinReportLevelCount("пл. уч.", 1), new KurinReportLevelCount("Разом", 1)]));

        Assert.StartsWith("%PDF-1.", body);
        Assert.Contains("%%EOF", body);
    }

    /// <summary>A kurin with tags gets a column for them and a line on each картотека page.</summary>
    [Fact]
    public void Render_ShouldDrawTags_WhenTheKurinUsesThem()
    {
        var youth = Member(Guid.NewGuid(), "Юнак Тест", groupName: "Gurtok 1", tags: ["Новики", "Табір"]);
        var body = RenderToText(Report(youth: [youth], members: [youth], showsTags: true));

        Assert.StartsWith("%PDF-1.", body);
        Assert.Contains("%%EOF", body);
    }

    /// <summary>An empty kurin still has to produce a document rather than a blank page or a throw.</summary>
    [Fact]
    public void Render_ShouldSurviveAnEmptyKurin()
    {
        var body = RenderToText(Report(tally: [new KurinReportLevelCount("Разом", 0)]));

        Assert.StartsWith("%PDF-1.", body);
        Assert.Contains("%%EOF", body);
    }

    [Fact]
    public void Render_ShouldEmbedAvailableImages()
    {
        var groupKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();
        var image = OnePixelPng();
        var member = Member(memberKey, groupKey: groupKey, groupName: "Group A", photoBlobName: "member-photos/test.png", photo: image);

        var body = RenderToText(Report(
            groups:
            [
                new KurinReportGroup(
                    groupKey,
                    "Group A",
                    "Description",
                    "group-silhouettes/test.png",
                    image,
                    [],
                    [new KurinReportGroupMember(memberKey, "Test Member", "member@example.com", "+380000000000", null)])
            ],
            youth: [member],
            members: [member]));

        Assert.StartsWith("%PDF-1.", body);
        Assert.Contains("%%EOF", body);
    }

    private static byte[] OnePixelPng()
        => Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
