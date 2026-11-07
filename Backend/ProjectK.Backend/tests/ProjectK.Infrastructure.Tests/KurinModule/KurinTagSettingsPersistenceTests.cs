using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Infrastructure.DbContexts;
using Xunit;

namespace ProjectK.Infrastructure.Tests.KurinModule;

/// <summary>
/// «Кожен бачить свої теги» switched off on a kurin's very first save must stay off. With a database
/// default of true on a non-nullable bool, EF leaves <c>false</c> out of the INSERT as «not set», and
/// SQL Server writes the default instead — the switch came back on after a reload.
/// </summary>
public class KurinTagSettingsPersistenceTests
{
    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public void OwnTagsVisible_HasNoDatabaseDefault_SoFalseIsAlwaysWritten()
    {
        using var context = new AppDbContext(Options());
        var property = context.Model.FindEntityType(typeof(KurinTagSettings))!.FindProperty(nameof(KurinTagSettings.OwnTagsVisible))!;

        Assert.Null(property.FindAnnotation(Microsoft.EntityFrameworkCore.Metadata.RelationalAnnotationNames.DefaultValue));
        Assert.Null(property.GetDefaultValueSql());
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never, property.ValueGenerated);
    }

    [Fact]
    public async Task OwnTagsVisible_SavedOffOnTheFirstInsert_ReadsBackOff()
    {
        var options = Options();
        var kurinKey = Guid.NewGuid();

        await using (var context = new AppDbContext(options))
        {
            context.KurinTagSettings.Add(new KurinTagSettings { KurinKey = kurinKey, OwnTagsVisible = false });
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var stored = await context.KurinTagSettings.AsNoTracking().SingleAsync(s => s.KurinKey == kurinKey);
            Assert.False(stored.OwnTagsVisible);
        }
    }
}
