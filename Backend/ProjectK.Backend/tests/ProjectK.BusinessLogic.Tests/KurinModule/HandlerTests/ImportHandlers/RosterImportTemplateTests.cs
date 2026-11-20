using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.ImportHandlers;

/// <summary>
/// The template and the mapping screen come from one set of rules per branch. These hold them
/// together: a template filled in and uploaded maps itself, and asks what that kurin's import asks.
/// </summary>
public class RosterImportTemplateTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _kurinData = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<ISpreadsheetWriter> _writer = new();
    private readonly Mock<ISpreadsheetReader> _reader = new();
    private IReadOnlyList<SheetTable> _written = [];

    public RosterImportTemplateTests()
    {
        _kurinData.Setup(u => u.Kurins).Returns(_kurins.Object);
        _writer
            .Setup(w => w.Write(It.IsAny<IReadOnlyList<SheetTable>>()))
            .Callback<IReadOnlyList<SheetTable>>(sheets => _written = sheets)
            .Returns([1]);
    }

    private void KurinOf(KurinBranch branch)
        => _kurins
            .Setup(k => k.GetByKeyAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(7, branch) { KurinKey = KurinKey });

    private async Task<IReadOnlyList<string>> TemplateHeadings()
    {
        var result = await new GetRosterImportTemplateQueryHandler(_kurinData.Object, _writer.Object)
            .Handle(new GetRosterImportTemplateQuery(KurinKey), CancellationToken.None);

        result.Data!.FileName.Should().EndWith(".xlsx");
        return _written.Single().Headers;
    }

    private async Task<RosterPreview> Preview(IReadOnlyList<string> headings)
    {
        _reader
            .Setup(r => r.Read(It.IsAny<Stream>(), It.IsAny<int>()))
            .Returns([new SheetRow(1, [.. headings]), new SheetRow(2, [.. headings.Select(_ => "x")])]);

        var result = await new PreviewRosterImportQueryHandler(_reader.Object, _kurinData.Object)
            .Handle(new PreviewRosterImportQuery(KurinKey, Stream.Null), CancellationToken.None);
        return result.Data!;
    }

    [Theory]
    [InlineData(KurinBranch.UPYu)]
    [InlineData(KurinBranch.USP)]
    [InlineData(KurinBranch.UPS)]
    public async Task AFilledTemplate_MapsItself_AndCoversEverythingRequired(KurinBranch branch)
    {
        KurinOf(branch);

        var preview = await Preview(await TemplateHeadings());

        var suggested = preview.Columns.Select(column => column.SuggestedField).ToList();
        suggested.Should().NotContain(RosterField.Ignore);
        suggested.Should().OnlyHaveUniqueItems();
        suggested.Should().Contain(preview.Shape.Required);
    }

    [Fact]
    public async Task ASeniorKurin_IsAskedForNoGurtok()
    {
        KurinOf(KurinBranch.USP);

        (await TemplateHeadings()).Should().NotContain("Гурток");
        var preview = await Preview(["Прізвище", "Гурток"]);

        preview.Shape.PlacesInGroups.Should().BeFalse();
        preview.Shape.Required.Should().NotContain(RosterField.GroupName);
        preview.Shape.LevelDates.Should().Contain(PlastLevel.SeniorPratsi, "a senior kurin is often УСП and УПС at once");
        preview.Columns[1].SuggestedField.Should().Be(RosterField.Ignore);
    }

    [Fact]
    public async Task AYouthKurin_IsAskedForAGurtok()
    {
        KurinOf(KurinBranch.UPYu);

        (await TemplateHeadings()).Should().Contain("Гурток");
        var preview = await Preview(["Гурток"]);

        preview.Shape.PlacesInGroups.Should().BeTrue();
        preview.Shape.Required.Should().Contain(RosterField.GroupName);
        preview.Columns[0].SuggestedField.Should().Be(RosterField.GroupName);
    }
}
