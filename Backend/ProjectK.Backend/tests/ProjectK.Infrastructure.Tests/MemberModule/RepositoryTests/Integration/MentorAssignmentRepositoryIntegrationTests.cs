using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.MemberModule;

namespace ProjectK.Infrastructure.Tests.MemberModule.RepositoryTests.Integration;

public class MentorAssignmentRepositoryIntegrationTests
{
    [Fact]
    public async Task GetActiveGroupsAsync_NamesOnlyCurrentAssignmentsInThatKurin()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);

        var mentor = Guid.NewGuid();
        var here = new Kurin(1);
        var elsewhere = new Kurin(2);
        var bravo = new Group("Bravo", here.KurinKey);
        var alpha = new Group("Alpha", here.KurinKey);
        var revoked = new Group("Revoked", here.KurinKey);
        var foreign = new Group("Foreign", elsewhere.KurinKey);
        context.AddRange(here, elsewhere, bravo, alpha, revoked, foreign);
        context.MentorAssignments.AddRange(
            new MentorAssignment { MentorUserKey = mentor, GroupKey = bravo.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = mentor, GroupKey = alpha.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = mentor, GroupKey = revoked.GroupKey, AssignedAtUtc = DateTime.UtcNow, RevokedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = mentor, GroupKey = foreign.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = Guid.NewGuid(), GroupKey = alpha.GroupKey, AssignedAtUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var names = await new MentorAssignmentRepository(context).GetActiveGroupsAsync(mentor, here.KurinKey);

        Assert.Equal(["Alpha", "Bravo"], names.Select(g => g.Name));
        Assert.Equal(alpha.GroupKey, names[0].GroupKey);
    }
}
