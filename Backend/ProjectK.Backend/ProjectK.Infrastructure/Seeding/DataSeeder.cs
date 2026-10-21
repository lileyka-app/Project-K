using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Seeding;

public static class DataSeeder
{
    /// <summary>
    /// The address of the administrator this file creates for the tiers that seed demo data. It is
    /// the only administrator whose password lives in the repository, so everything below tells it
    /// apart from an administrator the instance was configured with.
    /// </summary>
    private const string SeededAdministratorEmail = "admin@projectk.com";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        // 1. Seed the two roles the identity store still holds. Office roles are not among them:
        // what an office grants is worked out from the registry for the kurin the account is in,
        // and storing it on the account is what used to make it true everywhere at once.
        foreach (var roleName in new[] { SystemRole.Admin, SystemRole.Member })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new AppRole(roleName));
            }
        }

        // 2. Demo accounts exist only where demo data does, and demo data exists only on the two
        // tiers that are meant to start from the same point every time: Development and E2E.
        // The well-known administrator used to be created in production too, with the password
        // that sits in this file; a deployed instance now gets its first administrator through the
        // setup wizard instead, and this repository holds no password that opens one.
        //
        // Staging and Tailscale used to be on the demo list as well, and it cost real data: the
        // reset below wipes kurin 1 outright, so whatever people entered on a stand shown to
        // them was gone at the next `dev.sh up`, and every account that stood in that kurin was
        // left pointing at a key that no longer existed (STAB-07, STAB-05). Those tiers now keep
        // what they have and get their first administrator the way production does.
        // Demo is the public stand: it seeds like Development and is put back to this state every
        // night by DemoResetBackgroundService.
        var seedsDemoData = env.IsDevelopment() || env.EnvironmentName == "E2E" || env.EnvironmentName == "Demo";
        if (!seedsDemoData)
        {
            await WarnWhenNoAdministratorAsync(scope.ServiceProvider, userManager);
            return;
        }

        await EnsureSeededAdministratorAsync(scope.ServiceProvider, userManager);

        // 3. The load-test account: passwordless, reachable only through LoadTestLoginKey.
        await EnsurePasswordlessUser(userManager, "loadtest@projectk.com", "Load", "Tester", UserRole.Member);

        await ResetKurin1DataAsync(dbContext, userManager);

        // 4. Seed comprehensive demo data (kurin, groups, mentors, members)
        var demoSeeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
        await demoSeeder.SeedAsync();

        // Named fixtures the Playwright suite addresses directly. E2E only: they live in their own
        // kurin and must not leak into demo data for other environments.
        if (env.EnvironmentName == "E2E")
        {
            await E2eFixtureSeeder.SeedAsync(scope.ServiceProvider);
        }
    }

    /// <summary>
    /// Creates the well-known administrator for a tier that seeds demo data, unless this instance
    /// already has an administrator of its own.
    /// </summary>
    /// <remarks>
    /// <see cref="EnsureUser"/> recognises an account by its address and nothing else, so moving the
    /// real administrator to a real address left nothing at <see cref="SeededAdministratorEmail"/> —
    /// and the next start quietly created a second administrator there, with the password that sits
    /// in this file. The tier gate above keeps that out of a deployed instance; this keeps it out of
    /// any database that already has an administrator, whichever tier the process believes it is.
    /// </remarks>
    internal static async Task EnsureSeededAdministratorAsync(IServiceProvider services, UserManager<AppUser> userManager)
    {
        if (await HasConfiguredAdministratorAsync(userManager))
        {
            services.GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(DataSeeder))
                .LogInformation(
                    "An administrator is already configured; the seeded {Email} account is not created.",
                    SeededAdministratorEmail);
            return;
        }

        await EnsureUser(userManager, SeededAdministratorEmail, "System", "Admin", UserRole.Admin, "Admin@12345");
    }

    /// <summary>
    /// True when the administrator role is held under any address other than the one this file
    /// seeds — an administrator the instance was configured with, rather than one it was born with.
    /// </summary>
    internal static async Task<bool> HasConfiguredAdministratorAsync(UserManager<AppUser> userManager)
    {
        var administrators = await userManager.GetUsersInRoleAsync(SystemRole.Admin);
        return administrators.Any(administrator => !IsSeededAdministrator(administrator));
    }

    private static bool IsSeededAdministrator(AppUser user) =>
        string.Equals(user.Email, SeededAdministratorEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Said out loud at startup, because a deployment with no administrator is one nobody can
    /// manage until somebody opens the setup wizard — and the log is where that is first noticed.
    /// </summary>
    private static async Task WarnWhenNoAdministratorAsync(IServiceProvider services, UserManager<AppUser> userManager)
    {
        var administrators = await userManager.GetUsersInRoleAsync(SystemRole.Admin);
        if (administrators.Count > 0)
        {
            return;
        }

        services.GetService<ILoggerFactory>()
            ?.CreateLogger(nameof(DataSeeder))
            .LogWarning("No administrator account exists yet; the first visitor is sent to the setup wizard to create one.");
    }

    private static async Task ResetKurin1DataAsync(AppDbContext dbContext, UserManager<AppUser> userManager)
    {
        // The seeder's kurin is number 1 of Україна. One moved to another край by hand on a dev
        // database still holds the seeded accounts, and left alone it made the next run reuse them
        // on top of the old rows; with no Ukrainian kurin 1, that one is the seeder's.
        var kurin1 = await dbContext.Kurins
            .Where(k => k.Number == 1)
            .OrderBy(k => k.KraiCode == Krai.Ukraine ? 0 : 1)
            .FirstOrDefaultAsync();
        if (kurin1 == null)
        {
            return;
        }

        var kurinKey = kurin1.KurinKey;
        var groupKeys = await dbContext.Groups
            .Where(g => g.KurinKey == kurinKey)
            .Select(g => g.GroupKey)
            .ToListAsync();

        var memberKeys = await dbContext.Memberships
            .Where(ms => ms.KurinKey == kurinKey)
            .Select(ms => ms.MemberKey)
            .Distinct()
            .ToListAsync();

        // An administrator is not demo data. The seeded one was spared here by address alone, which
        // spared exactly the account that is cheapest to recreate and none of the ones that are not:
        // an administrator configured by hand, who happens to stand in kurin 1, was deleted outright.
        var administratorIds = (await userManager.GetUsersInRoleAsync(SystemRole.Admin))
            .Select(administrator => administrator.Id)
            .ToHashSet();

        // Whose accounts these are is said by membership: the account itself no longer names a kurin.
        var accountKeysHere = await dbContext.Memberships
            .Where(ms => ms.KurinKey == kurinKey && ms.UserKey != null)
            .Select(ms => ms.UserKey!.Value)
            .Distinct()
            .ToListAsync();

        var usersToDelete = await userManager.Users
            .Where(u => accountKeysHere.Contains(u.Id))
            .ToListAsync();

        var deletedUsers = usersToDelete.Where(u => !administratorIds.Contains(u.Id)).ToList();
        foreach (var user in deletedUsers)
        {
            await userManager.DeleteAsync(user);
        }

        // Their queue entries go too (invitations cascade with them). Left behind, an entry keeps
        // its address "waiting", and adding someone with it again was refused as a conflict.
        var deletedEmails = deletedUsers
            .Where(user => user.Email is not null)
            .Select(user => user.Email!)
            .ToList();
        dbContext.WaitlistEntries.RemoveRange(
            await dbContext.WaitlistEntries.Where(entry => deletedEmails.Contains(entry.Email)).ToListAsync());

        await RemovePlanningAsync(dbContext, kurinKey);

        await RemoveProgressAsync(dbContext, kurinKey, memberKeys);

        var plastLevelHistories = await dbContext.PlastLevelHistories
            .Where(p => memberKeys.Contains(p.MemberKey))
            .ToListAsync();
        dbContext.PlastLevelHistories.RemoveRange(plastLevelHistories);

        var memberAwards = await dbContext.MemberAwards
            .Where(a => a.KurinKey == kurinKey || memberKeys.Contains(a.MemberKey))
            .ToListAsync();
        dbContext.MemberAwards.RemoveRange(memberAwards);

        var memberWarnings = await dbContext.MemberWarnings
            .Where(w => memberKeys.Contains(w.MemberKey))
            .ToListAsync();
        dbContext.MemberWarnings.RemoveRange(memberWarnings);

        var leadershipKeys = await dbContext.Leaderships
            .Where(l => l.KurinKey == kurinKey || (l.GroupKey != null && groupKeys.Contains(l.GroupKey.Value)))
            .Select(l => l.LeadershipKey)
            .ToListAsync();

        if (leadershipKeys.Count > 0)
        {
            var leadershipHistories = await dbContext.LeadershipHistories
                .Where(h => memberKeys.Contains(h.MemberKey) || leadershipKeys.Contains(h.LeadershipKey))
                .ToListAsync();
            dbContext.LeadershipHistories.RemoveRange(leadershipHistories);
        }

        var leaderships = await dbContext.Leaderships
            .Where(l => l.KurinKey == kurinKey || (l.GroupKey != null && groupKeys.Contains(l.GroupKey.Value)))
            .ToListAsync();
        dbContext.Leaderships.RemoveRange(leaderships);

        var mentorAssignments = await dbContext.MentorAssignments
            .Where(a => groupKeys.Contains(a.GroupKey))
            .ToListAsync();
        dbContext.MentorAssignments.RemoveRange(mentorAssignments);

        // The agenda is seeded too, and unlike people it is not keyed on anything that would
        // stop a second run from adding the same сходини again — so it is wiped with the rest.
        await RemoveAgendaAsync(dbContext, kurinKey);

        // Dues and score hold the kurin by key with no foreign key, so removing the kurin leaves
        // them behind; the next run would add a second year of вкладка and points on top.
        dbContext.DuesEntries.RemoveRange(await dbContext.DuesEntries.Where(e => e.KurinKey == kurinKey).ToListAsync());
        dbContext.DuesCharges.RemoveRange(await dbContext.DuesCharges.Where(c => c.KurinKey == kurinKey).ToListAsync());
        dbContext.DuesConcessions.RemoveRange(await dbContext.DuesConcessions.Where(c => c.KurinKey == kurinKey).ToListAsync());
        dbContext.DuesConcessionGrants.RemoveRange(await dbContext.DuesConcessionGrants.Where(g => g.KurinKey == kurinKey).ToListAsync());
        dbContext.GroupDuesRates.RemoveRange(await dbContext.GroupDuesRates.Where(r => r.KurinKey == kurinKey).ToListAsync());
        dbContext.KurinDuesRates.RemoveRange(await dbContext.KurinDuesRates.Where(r => r.KurinKey == kurinKey).ToListAsync());

        dbContext.KurinScoreSettings.RemoveRange(await dbContext.KurinScoreSettings.Where(s => s.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreRules.RemoveRange(await dbContext.ScoreRules.Where(r => r.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreAttendanceRates.RemoveRange(await dbContext.ScoreAttendanceRates.Where(r => r.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreItems.RemoveRange(await dbContext.ScoreItems.Where(i => i.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreStages.RemoveRange(await dbContext.ScoreStages.Where(s => s.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreAttendances.RemoveRange(await dbContext.ScoreAttendances.Where(a => a.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreEntries.RemoveRange(await dbContext.ScoreEntries.Where(e => e.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreGroupMoves.RemoveRange(await dbContext.ScoreGroupMoves.Where(m => m.KurinKey == kurinKey).ToListAsync());
        dbContext.ScoreTrailEvents.RemoveRange(await dbContext.ScoreTrailEvents.Where(e => e.KurinKey == kurinKey).ToListAsync());
        dbContext.PrivateScoreEntries.RemoveRange(await dbContext.PrivateScoreEntries.Where(e => e.KurinKey == kurinKey).ToListAsync());
        dbContext.PrivateScoreCriteria.RemoveRange(await dbContext.PrivateScoreCriteria.Where(c => c.KurinKey == kurinKey).ToListAsync());

        // The seeder wipes its own demo kurin outright, people included — the reset exists to
        // give every run the same starting point, and these are not real people.
        var memberships = await dbContext.Memberships
            .Where(ms => ms.KurinKey == kurinKey)
            .ToListAsync();
        dbContext.Memberships.RemoveRange(memberships);

        var members = await dbContext.Members
            .Where(m => memberKeys.Contains(m.MemberKey))
            .ToListAsync();
        dbContext.Members.RemoveRange(members);

        var groups = await dbContext.Groups
            .Where(g => g.KurinKey == kurinKey)
            .ToListAsync();
        dbContext.Groups.RemoveRange(groups);

        dbContext.Kurins.Remove(kurin1);

        await dbContext.SaveChangesAsync();
    }

    private static async Task RemovePlanningAsync(AppDbContext dbContext, Guid kurinKey)
    {
        var planningSessionKeys = await dbContext.PlanningSessions
            .Where(s => s.KurinKey == kurinKey)
            .Select(s => s.PlanningSessionKey)
            .ToListAsync();

        var planningParticipantKeys = await dbContext.PlanningParticipants
            .Where(p => planningSessionKeys.Contains(p.PlanningSessionKey))
            .Select(p => p.PlanningParticipantKey)
            .ToListAsync();

        if (planningParticipantKeys.Count > 0)
        {
            var busyRanges = await dbContext.ParticipantBusyRanges
                .Where(r => planningParticipantKeys.Contains(r.PlanningParticipantKey))
                .ToListAsync();
            dbContext.ParticipantBusyRanges.RemoveRange(busyRanges);
        }

        if (planningSessionKeys.Count > 0)
        {
            var planningParticipants = await dbContext.PlanningParticipants
                .Where(p => planningSessionKeys.Contains(p.PlanningSessionKey))
                .ToListAsync();
            dbContext.PlanningParticipants.RemoveRange(planningParticipants);

            var planningSessions = await dbContext.PlanningSessions
                .Where(s => s.KurinKey == kurinKey)
                .ToListAsync();
            dbContext.PlanningSessions.RemoveRange(planningSessions);
        }
    }

    private static async Task RemoveProgressAsync(AppDbContext dbContext, Guid kurinKey, List<Guid> memberKeys)
    {
        var badgeProgressKeys = await dbContext.BadgeProgresses
            .Where(p => p.KurinKey == kurinKey || memberKeys.Contains(p.MemberKey))
            .Select(p => p.BadgeProgressKey)
            .ToListAsync();

        if (badgeProgressKeys.Count > 0)
        {
            var badgeAuditEvents = await dbContext.BadgeProgressAuditEvents
                .Where(e => badgeProgressKeys.Contains(e.BadgeProgressKey))
                .ToListAsync();
            dbContext.BadgeProgressAuditEvents.RemoveRange(badgeAuditEvents);
        }

        var badgeProgresses = await dbContext.BadgeProgresses
            .Where(p => p.KurinKey == kurinKey || memberKeys.Contains(p.MemberKey))
            .ToListAsync();
        dbContext.BadgeProgresses.RemoveRange(badgeProgresses);

        var probeProgressKeys = await dbContext.ProbeProgresses
            .Where(p => p.KurinKey == kurinKey || memberKeys.Contains(p.MemberKey))
            .Select(p => p.ProbeProgressKey)
            .ToListAsync();

        if (probeProgressKeys.Count > 0)
        {
            var probeAuditEvents = await dbContext.ProbeProgressAuditEvents
                .Where(e => probeProgressKeys.Contains(e.ProbeProgressKey))
                .ToListAsync();
            dbContext.ProbeProgressAuditEvents.RemoveRange(probeAuditEvents);
        }

        var probePointProgresses = await dbContext.ProbePointProgresses
            .Where(p => p.KurinKey == kurinKey || memberKeys.Contains(p.MemberKey))
            .ToListAsync();
        dbContext.ProbePointProgresses.RemoveRange(probePointProgresses);

        var probeProgresses = await dbContext.ProbeProgresses
            .Where(p => p.KurinKey == kurinKey || memberKeys.Contains(p.MemberKey))
            .ToListAsync();
        dbContext.ProbeProgresses.RemoveRange(probeProgresses);
    }

    private static async Task RemoveAgendaAsync(AppDbContext dbContext, Guid kurinKey)
    {
        var agendaItemKeys = await dbContext.AgendaItems
            .Where(a => a.KurinKey == kurinKey)
            .Select(a => a.AgendaItemKey)
            .ToListAsync();

        if (agendaItemKeys.Count > 0)
        {
            var agendaResponses = await dbContext.AgendaResponses
                .Where(r => agendaItemKeys.Contains(r.AgendaItemKey))
                .ToListAsync();
            dbContext.AgendaResponses.RemoveRange(agendaResponses);

            var agendaAssignments = await dbContext.AgendaAssignments
                .Where(a => agendaItemKeys.Contains(a.AgendaItemKey))
                .ToListAsync();
            dbContext.AgendaAssignments.RemoveRange(agendaAssignments);

            var agendaItems = await dbContext.AgendaItems
                .Where(a => a.KurinKey == kurinKey)
                .ToListAsync();
            dbContext.AgendaItems.RemoveRange(agendaItems);
        }

        var agendaCategories = await dbContext.AgendaCategories
            .Where(c => c.KurinKey == kurinKey)
            .ToListAsync();
        dbContext.AgendaCategories.RemoveRange(agendaCategories);
    }

    private static async Task<AppUser?> EnsurePasswordlessUser(UserManager<AppUser> userManager, string email, string firstName, string lastName, UserRole role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                OnboardingStatus = OnboardingStatus.Active
            };

            // Create user without a password
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create the account {email} (activation pending): {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role.ToClaimValue());
        }
        return user;
    }

    /// <summary>
    /// The password every seeded demo/fixture account gets. Seeded accounts exist only in
    /// non-production environments.
    /// </summary>
    internal const string SeededPassword = "User@12345";

    /// <summary>Finds the named group in the kurin, creating it when it is missing.</summary>
    internal static async Task<Group> EnsureGroupAsync(
        AppDbContext dbContext,
        string name,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
    {
        var group = await dbContext.Groups
            .FirstOrDefaultAsync(g => g.Name == name && g.KurinKey == kurinKey, cancellationToken);
        if (group == null)
        {
            group = new Group(name, kurinKey);
            dbContext.Groups.Add(group);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return group;
    }

    /// <summary>
    /// Everything a seeder says about one person. The optional part is what tells a юнак from the
    /// виховник who runs their гурток: the kind of membership, when it began, and the details a
    /// картка shows that fixtures never needed.
    /// </summary>
    internal sealed record SeededPerson(
        string Email,
        string FirstName,
        string? MiddleName,
        string LastName,
        Guid KurinKey,
        Guid? GroupKey,
        string PhoneNumber,
        DateOnly DateOfBirth,
        MembershipKind Kind = MembershipKind.Youth,
        DateTime? JoinedAtUtc = null,
        string? Address = null,
        string? School = null);

    /// <summary>
    /// Finds the member by email, creating the member row and its linked account when missing.
    /// Keyed on email so re-running a seeder does not give one account a second member row.
    /// </summary>
    internal static Task<Member> EnsureMemberAsync(
        AppDbContext dbContext,
        UserManager<AppUser> userManager,
        string email,
        string firstName,
        string lastName,
        Guid kurinKey,
        Guid? groupKey,
        string phoneNumber,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        return EnsureMemberAsync(
            dbContext,
            userManager,
            new SeededPerson(email, firstName, null, lastName, kurinKey, groupKey, phoneNumber, dateOfBirth),
            cancellationToken);
    }

    internal static async Task<Member> EnsureMemberAsync(
        AppDbContext dbContext,
        UserManager<AppUser> userManager,
        SeededPerson person,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUser(userManager, person.Email, person.FirstName, person.LastName, UserRole.Member, SeededPassword);

        // The account settings page reads the phone from the account, not the card.
        if (!string.Equals(user!.PhoneNumber, person.PhoneNumber, StringComparison.Ordinal))
        {
            user.PhoneNumber = person.PhoneNumber;
            await userManager.UpdateAsync(user);
        }

        var member = await dbContext.Members.FirstOrDefaultAsync(m => m.Email == person.Email, cancellationToken);
        if (member == null)
        {
            member = new Member
            {
                FirstName = person.FirstName,
                MiddleName = person.MiddleName,
                LastName = person.LastName,
                Email = person.Email,
                PhoneNumber = person.PhoneNumber,
                DateOfBirth = person.DateOfBirth,
                Address = person.Address,
                School = person.School,
                UserKey = user.Id
            };
            dbContext.Members.Add(member);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Seeded people are written straight to the tables, so nothing announces where they were
        // put. Without this row they exist and belong to no kurin, which is to say they are in
        // no list and in nobody's reach.
        var alreadyPlaced = await dbContext.Memberships.AnyAsync(
            ms => ms.MemberKey == member.MemberKey && ms.LeftAtUtc == null,
            cancellationToken);
        if (!alreadyPlaced)
        {
            dbContext.Memberships.Add(new Membership
            {
                MemberKey = member.MemberKey,
                UserKey = member.UserKey,
                KurinKey = person.KurinKey,
                GroupKey = person.GroupKey,
                Kind = person.Kind,
                JoinedAtUtc = person.JoinedAtUtc ?? DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return member;
    }

    /// <summary>Seats the member in the office unless they already hold it.</summary>
    internal static void AddOffice(Leadership leadership, Guid memberKey, LeadershipRole role)
    {
        var alreadyHolds = leadership.LeadershipHistories
            .Any(h => h.MemberKey == memberKey && h.Role == role && h.EndDate == null);
        if (!alreadyHolds)
        {
            leadership.LeadershipHistories.Add(new LeadershipHistory
            {
                MemberKey = memberKey,
                Role = role,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        }
    }

    internal static async Task<AppUser?> EnsureUser(UserManager<AppUser> userManager, string email, string firstName, string lastName, UserRole role, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                OnboardingStatus = OnboardingStatus.Active
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role.ToClaimValue());
        }
        return user;
    }
}
