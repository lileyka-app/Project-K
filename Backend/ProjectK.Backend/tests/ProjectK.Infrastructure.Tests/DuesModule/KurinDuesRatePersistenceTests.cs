using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.DuesModule;
using Xunit;

namespace ProjectK.Infrastructure.Tests.DuesModule;

/// <summary>
/// Rates are read untracked and saved back with Update, so the статті live in a plain JSON column
/// rather than an owned collection, which cannot be saved that way.
/// </summary>
public class KurinDuesRatePersistenceTests
{
    [Fact]
    public async Task ArticlesOfARateReadUntracked_AreReplacedOnUpdate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var kurinKey = Guid.NewGuid();

        await using (var context = new AppDbContext(options))
        {
            var rate = new KurinDuesRate { KurinKey = kurinKey, FromQuarter = 8104 };
            rate.SetArticles([new KurinDuesArticle { Name = "Станиця", Recipient = DuesRecipient.Stanytsia, Amount = 240, ReducedAmount = 180 }]);
            context.KurinDuesRates.Add(rate);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var repository = new KurinDuesRateRepository(context);
            var rate = (await repository.GetForKurinAsync(kurinKey)).Single();
            rate.SetArticles(
            [
                new KurinDuesArticle { Name = "Станиця", Recipient = DuesRecipient.Stanytsia, Amount = 240, ReducedAmount = 180 },
                new KurinDuesArticle { Name = "Табірний фонд", Recipient = DuesRecipient.Kurin, Amount = 50, ReducedAmount = 25 }
            ]);
            repository.Update(rate);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var rate = await context.KurinDuesRates.AsNoTracking().SingleAsync();

            Assert.Equal(["Станиця", "Табірний фонд"], rate.Articles.Select(a => a.Name));
            Assert.Equal(50, rate.KurinShare);
            Assert.Equal(25, rate.KurinReduced);
        }
    }
}
