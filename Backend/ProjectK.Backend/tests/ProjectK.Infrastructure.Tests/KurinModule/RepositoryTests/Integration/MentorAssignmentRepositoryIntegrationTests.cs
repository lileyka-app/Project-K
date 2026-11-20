using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.KurinModule;

namespace ProjectK.Infrastructure.Tests.KurinModule.RepositoryTests.Integration;

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

    /// <summary>
    /// The people behind the assignments in force of one kurin, once each and without an office: a
    /// revoked assignment, one in another kurin and an assignment with no person behind it name nobody.
    /// </summary>
    [Fact]
    public async Task GetActiveMentorsInKurinAsync_NamesEachCurrentMentorOfThatKurinOnce_WithoutAnOffice()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().AddAppDbContextInterceptors()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);

        var here = new Kurin(1);
        var elsewhere = new Kurin(2);
        var alpha = new Group("Alpha", here.KurinKey);
        var bravo = new Group("Bravo", here.KurinKey);
        var foreign = new Group("Foreign", elsewhere.KurinKey);
        Member Person(string name) => new() { MemberKey = Guid.NewGuid(), UserKey = Guid.NewGuid(), FirstName = name, LastName = "Виховна", Email = $"{name}@projectk.com", PhoneNumber = "+380500000000" };
        var both = Person("both");
        var handedOver = Person("handed");
        var abroad = Person("abroad");
        context.AddRange(here, elsewhere, alpha, bravo, foreign, both, handedOver, abroad);
        context.MentorAssignments.AddRange(
            new MentorAssignment { MentorUserKey = both.UserKey!.Value, GroupKey = alpha.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = both.UserKey!.Value, GroupKey = bravo.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = handedOver.UserKey!.Value, GroupKey = alpha.GroupKey, AssignedAtUtc = DateTime.UtcNow, RevokedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = abroad.UserKey!.Value, GroupKey = foreign.GroupKey, AssignedAtUtc = DateTime.UtcNow },
            new MentorAssignment { MentorUserKey = Guid.NewGuid(), GroupKey = bravo.GroupKey, AssignedAtUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repository = new MentorAssignmentRepository(context);
        var mentors = await repository.GetActiveMentorsInKurinAsync(here.KurinKey);

        var mentor = Assert.Single(mentors);
        Assert.Equal(both.MemberKey, mentor.MemberKey);
        Assert.Null(mentor.Office);
        Assert.True(await repository.HasActiveInKurinAsync(both.UserKey!.Value, here.KurinKey));
        Assert.False(await repository.HasActiveInKurinAsync(handedOver.UserKey!.Value, here.KurinKey));
        Assert.False(await repository.HasActiveInKurinAsync(abroad.UserKey!.Value, here.KurinKey));
    }
}
