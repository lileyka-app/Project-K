using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The kurin's «Права й видимість»: defaults, the overlay of what a kurin set, how it turns into
/// permissions, and what may and may not be stored.
/// </summary>
public class KurinAccessPolicyTests
{
    private static readonly string Zvyazkovyi = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string Vykhovnyk = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    private static readonly string Hurtkovyy = SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy);

    private static IEnumerable<string> EveryRole() =>
        LeadershipOffices.All()
            .Select(office => SystemRole.ForOffice(office.Type, office.Role))
            .Append(SystemRole.Member)
            .Append(SystemRole.Admin);

    private static Dictionary<string, IReadOnlyDictionary<string, string?>> Matrix(
        params (KurinCapability Capability, PolicyAudience Audience, CapabilityReach Reach)[] cells) =>
        cells
            .GroupBy(cell => cell.Capability)
            .ToDictionary(
                row => row.Key.ToString(),
                row => (IReadOnlyDictionary<string, string?>)row.ToDictionary(
                    cell => cell.Audience.ToString(),
                    cell => (string?)cell.Reach.ToString()));

    private static KurinAccessPolicy Set(params (KurinCapability, PolicyAudience, CapabilityReach)[] cells) =>
        KurinAccessPolicy.FromMatrix(Matrix(cells));

    // Every cell a kurin may change, opened to the whole kurin.
    private static KurinAccessPolicy WideOpen() =>
        Set(KurinCapabilities.All
            .SelectMany(descriptor => KurinCapabilities.Audiences
                .Where(audience => !descriptor.IsLocked(audience))
                .Select(audience => (descriptor.Key, audience, CapabilityReach.Kurin)))
            .ToArray());

    // Every cell a kurin may change, closed.
    private static KurinAccessPolicy Closed() =>
        Set(KurinCapabilities.All
            .SelectMany(descriptor => KurinCapabilities.Audiences
                .Where(audience => !descriptor.IsLocked(audience))
                .Select(audience => (descriptor.Key, audience, CapabilityReach.None)))
            .ToArray());

    [Fact]
    public void EveryCapability_HasOneRow_WithADefaultForEveryAudience()
    {
        Assert.Equal(Enum.GetValues<KurinCapability>(), KurinCapabilities.All.Select(descriptor => descriptor.Key));
        Assert.All(KurinCapabilities.All, descriptor =>
            Assert.Equal(Enum.GetValues<PolicyAudience>(), descriptor.Defaults.Keys.Order()));
    }

    [Fact]
    public void TheZvyazkovyi_IsLockedToTheWholeKurin_OnEverything()
    {
        Assert.All(KurinCapabilities.All, descriptor =>
        {
            Assert.True(descriptor.IsLocked(PolicyAudience.Zvyazkovyi));
            Assert.Equal(CapabilityReach.Kurin, descriptor.Defaults[PolicyAudience.Zvyazkovyi]);
        });
    }

    [Fact]
    public void OnlyDataRows_LetTheYouthColumnBeSet_AndActionsNeverReachYouths()
    {
        Assert.All(KurinCapabilities.All, descriptor =>
        {
            Assert.Equal(descriptor.Group == CapabilityGroup.MemberData, descriptor.YouthConfigurable);
            if (!descriptor.YouthConfigurable)
            {
                Assert.Equal(CapabilityReach.None, descriptor.Defaults[PolicyAudience.Youth]);
                Assert.True(descriptor.IsLocked(PolicyAudience.Youth));
            }
        });
    }

    [Theory]
    [InlineData(ResourceType.Group)]
    [InlineData(ResourceType.AgendaItem)]
    [InlineData(ResourceType.PlanningSession)]
    public void ConfigurableCapabilities_NeverTouchCoarsePolicyResources(ResourceType coarse)
    {
        // The coarse request policies (RequireKurinManagement, RequireGroupLeadership, agenda and
        // planning authoring) are decided from roles alone, without a kurin. A capability on these
        // resources would make them disagree with the resource check.
        Assert.DoesNotContain(KurinCapabilities.All, descriptor => descriptor.Resource == coarse);

        foreach (var role in EveryRole())
        {
            foreach (var action in Enum.GetValues<ResourceAction>())
            {
                var byDefault = RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role]), coarse, action);
                Assert.Equal(byDefault, RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role], WideOpen()), coarse, action));
                Assert.Equal(byDefault, RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role], Closed()), coarse, action));
            }
        }
    }

    [Fact]
    public void Resolve_WithoutAPolicy_IsTheDefaultPolicy()
    {
        foreach (var role in EveryRole())
        {
            Assert.Equal(
                RolePermissionMap.Resolve([role], KurinAccessPolicy.Default).ToHashSet(),
                RolePermissionMap.Resolve([role]).ToHashSet());
        }
    }

    [Fact]
    public void StaticGrants_HoldNoConfigurablePair_ButTheSelfServiceOnes()
    {
        foreach (var role in EveryRole().Where(role => role != SystemRole.Admin))
        {
            var offending = RolePermissionMap.StaticGrants([role])
                .Where(grant => grant.Scope != AccessScope.Own && KurinCapabilities.IsConfigurable(grant.Resource, grant.Action))
                .Select(grant => grant.ToClaimValue())
                .ToList();

            Assert.True(offending.Count == 0, $"{role} is granted statically: {string.Join(", ", offending)}");
        }
    }

    [Fact]
    public void AClosedPolicy_KeepsTheSelfServiceGrants()
    {
        var permissions = RolePermissionMap.Resolve([SystemRole.Member], Closed());

        Assert.Contains(new Permission(ResourceType.MemberAward, ResourceAction.Delete, AccessScope.Own), permissions);
        Assert.Contains(new Permission(ResourceType.MemberAward, ResourceAction.Create, AccessScope.Own), permissions);
        Assert.Contains(new Permission(ResourceType.BadgeProgress, ResourceAction.Create, AccessScope.Own), permissions);
        Assert.DoesNotContain(permissions, permission => permission.Resource == ResourceType.MemberContacts);
    }

    [Fact]
    public void TheBaselineRead_LeavesOutWhatThePolicyDecides()
    {
        var permissions = RolePermissionMap.StaticGrants([SystemRole.Member]);

        foreach (var resource in new[]
                 {
                     ResourceType.MemberContacts, ResourceType.MemberBirthDate, ResourceType.MemberBirthday,
                     ResourceType.MemberAddress, ResourceType.MemberParents, ResourceType.MemberExtras,
                     ResourceType.KurinJournal
                 })
        {
            Assert.Null(RolePermissionMap.WidestScope(permissions, resource, ResourceAction.Read));
        }
    }

    [Fact]
    public void AdminGrants_CoverTheNewResources_WhateverThePolicy()
    {
        foreach (var resource in new[] { ResourceType.MemberContacts, ResourceType.MemberParents, ResourceType.KurinJournal })
        {
            foreach (var action in Enum.GetValues<ResourceAction>())
            {
                Assert.Equal(
                    AccessScope.KurinWide,
                    RolePermissionMap.WidestScope(RolePermissionMap.Resolve([SystemRole.Admin], Closed()), resource, action));
            }
        }
    }

    [Fact]
    public void APolicy_ChangesWhatTheRolesResolveTo()
    {
        var policy = Set(
            (KurinCapability.SeeContacts, PolicyAudience.Youth, CapabilityReach.OwnGroup),
            (KurinCapability.SeeContacts, PolicyAudience.Hurtkovyy, CapabilityReach.Kurin),
            (KurinCapability.ConfirmAward1, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward2, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward3, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward4, PolicyAudience.Vykhovnyk, CapabilityReach.None));

        AccessScope? Widest(string role, ResourceType resource, ResourceAction action) =>
            RolePermissionMap.WidestScope(RolePermissionMap.Resolve([SystemRole.Member, role], policy), resource, action);

        Assert.Equal(AccessScope.OwnGroups, Widest(SystemRole.Member, ResourceType.MemberContacts, ResourceAction.Read));
        Assert.Equal(AccessScope.KurinWide, Widest(Hurtkovyy, ResourceType.MemberContacts, ResourceAction.Read));
        Assert.Null(Widest(Vykhovnyk, ResourceType.MemberAward, ResourceAction.Update));
        Assert.Equal(AccessScope.KurinWide, Widest(Zvyazkovyi, ResourceType.MemberAward, ResourceAction.Update));
    }

    [Fact]
    public void LeveledCapabilities_GrantTheirPair_AtTheWidestLevel()
    {
        var policy = Set(
            (KurinCapability.GiveWarning1, PolicyAudience.Hurtkovyy, CapabilityReach.OwnGroup),
            (KurinCapability.GiveWarning3, PolicyAudience.Hurtkovyy, CapabilityReach.Kurin));

        var grants = policy.Grants([PolicyAudience.Hurtkovyy, PolicyAudience.Youth]).ToList();

        Assert.Equal(
            new[] { new Permission(ResourceType.MemberWarning, ResourceAction.Create, AccessScope.KurinWide) },
            grants.Where(grant => grant.Resource == ResourceType.MemberWarning));
        Assert.DoesNotContain(grants, grant => grant.Resource == ResourceType.MemberAward);
    }

    [Fact]
    public void Reach_IsTheWidestOverTheAudiencesHeld()
    {
        var policy = Set((KurinCapability.SeeAddress, PolicyAudience.Hurtkovyy, CapabilityReach.OwnGroup));

        Assert.Equal(CapabilityReach.None, policy.Reach(KurinCapability.SeeAddress, [PolicyAudience.Youth]));
        Assert.Equal(CapabilityReach.OwnGroup, policy.Reach(KurinCapability.SeeAddress, [PolicyAudience.Youth, PolicyAudience.Hurtkovyy]));
        Assert.Equal(CapabilityReach.Kurin, policy.Reach(KurinCapability.SeeAddress, [PolicyAudience.Hurtkovyy, PolicyAudience.Zvyazkovyi]));
        Assert.Equal(CapabilityReach.None, policy.Reach(KurinCapability.SeeAddress, []));
    }

    [Theory]
    [InlineData("Member", new[] { PolicyAudience.Youth })]
    [InlineData("KV.Zvyazkovyi", new[] { PolicyAudience.Zvyazkovyi, PolicyAudience.Youth })]
    [InlineData("Admin", new[] { PolicyAudience.Zvyazkovyi, PolicyAudience.Youth })]
    [InlineData("KV.Vykhovnyk", new[] { PolicyAudience.Vykhovnyk, PolicyAudience.Youth })]
    [InlineData("KV.Instruktor", new[] { PolicyAudience.Instruktor, PolicyAudience.Youth })]
    [InlineData("Kurin.Kurinnuy", new[] { PolicyAudience.Kurinnyy, PolicyAudience.Youth })]
    [InlineData("Kurin.Skarbnyk", new[] { PolicyAudience.KurinProvid, PolicyAudience.Youth })]
    [InlineData("Kurin.OtherKurin", new[] { PolicyAudience.KurinProvid, PolicyAudience.Youth })]
    [InlineData("Group.Hurtkoviy", new[] { PolicyAudience.Hurtkovyy, PolicyAudience.Youth })]
    [InlineData("Group.Pysar", new[] { PolicyAudience.GroupProvid, PolicyAudience.Youth })]
    public void PolicyAudiences_FoldEachOfficeIntoItsColumn(string role, PolicyAudience[] expected)
    {
        Assert.Equal(expected.Order(), PolicyAudiences.For([role]).Order());
    }

    [Fact]
    public void NoRoles_AreNoAudience()
    {
        Assert.Empty(PolicyAudiences.For([]));
        Assert.Empty(RolePermissionMap.Resolve([]));
    }

    [Fact]
    public void WriteThenRead_KeepsOnlyWhatDiffers()
    {
        var policy = Set(
            (KurinCapability.SeeContacts, PolicyAudience.Youth, CapabilityReach.OwnGroup),
            (KurinCapability.SeeContacts, PolicyAudience.Vykhovnyk, CapabilityReach.Kurin),
            (KurinCapability.ReadJournal, PolicyAudience.Kurinnyy, CapabilityReach.Kurin));

        var json = policy.Write();

        Assert.Equal(
            """{"v":1,"grants":{"SeeContacts":{"Youth":"OwnGroup"},"ReadJournal":{"Kurinnyy":"Kurin"}}}""",
            json);

        var read = KurinAccessPolicy.Read(json);
        Assert.Equal(CapabilityReach.OwnGroup, read.Reach(KurinCapability.SeeContacts, PolicyAudience.Youth));
        Assert.Equal(CapabilityReach.Kurin, read.Reach(KurinCapability.ReadJournal, PolicyAudience.Kurinnyy));
        Assert.Equal(CapabilityReach.Kurin, read.Reach(KurinCapability.SeeContacts, PolicyAudience.Hurtkovyy));
        Assert.Equal(json, read.Write());
    }

    [Fact]
    public void TheDefaults_AreWrittenAsNothing()
    {
        Assert.Null(KurinAccessPolicy.Default.Write());
        Assert.True(Set((KurinCapability.SeeContacts, PolicyAudience.Youth, CapabilityReach.Kurin)).IsDefault);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"v":1}""")]
    [InlineData("""{"v":1,"grants":[]}""")]
    public void AnEmptyDocument_IsTheDefaults(string? json)
    {
        Assert.True(KurinAccessPolicy.Read(json).IsDefault);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"v":2,"grants":{"SeeContacts":{"Youth":"None"}}}""")]
    [InlineData("""{"v":"1","grants":{"SeeContacts":{"Youth":"None"}}}""")]
    [InlineData("[]")]
    public void ADocumentItCannotRead_IsTheDefaults_AndSaysSo(string json)
    {
        var warnings = new List<string>();

        var policy = KurinAccessPolicy.Read(json, warnings.Add);

        Assert.True(policy.IsDefault);
        Assert.Single(warnings);
    }

    [Fact]
    public void Read_SkipsWhatItDoesNotKnow_AndWhatIsLocked()
    {
        const string json = """
            {
              "v": 1,
              "future": true,
              "grants": {
                "SeeFutureThing": { "Youth": "None" },
                "SeeContacts": { "Youth": "OwnGroup", "Martian": "None", "Hurtkovyy": "Galaxy", "Instruktor": 2 },
                "SeeBirthDate": { "Zvyazkovyi": "None" },
                "GiveWarning1": { "Youth": "Kurin" },
                "SeeAddress": { "Hurtkovyy": "None" },
                "ReadJournal": { "Kurinnyy": "kurin" }
              }
            }
            """;
        var warnings = new List<string>();

        var policy = KurinAccessPolicy.Read(json, warnings.Add);

        Assert.Empty(warnings);
        Assert.Equal(new[] { KurinCapability.SeeContacts }, policy.Overrides.Keys);
        Assert.Equal(new[] { PolicyAudience.Youth }, policy.Overrides[KurinCapability.SeeContacts].Keys);
        Assert.Equal(CapabilityReach.Kurin, policy.Reach(KurinCapability.SeeBirthDate, PolicyAudience.Zvyazkovyi));
        Assert.Equal(CapabilityReach.None, policy.Reach(KurinCapability.GiveWarning1, PolicyAudience.Youth));
        Assert.Equal(CapabilityReach.None, policy.Reach(KurinCapability.ReadJournal, PolicyAudience.Kurinnyy));
    }

    [Fact]
    public void Validate_AcceptsTheFullDefaultMatrix()
    {
        var full = KurinCapabilities.All
            .SelectMany(descriptor => KurinCapabilities.Audiences
                .Select(audience => (descriptor.Key, audience, descriptor.Defaults[audience])))
            .ToArray();

        Assert.Empty(KurinAccessPolicy.Validate(Matrix(full)));
        Assert.True(KurinAccessPolicy.FromMatrix(Matrix(full)).IsDefault);
    }

    [Fact]
    public void Validate_AcceptsTheYouthColumn_OnADataRow()
    {
        Assert.Empty(KurinAccessPolicy.Validate(Matrix((KurinCapability.SeeBirthDate, PolicyAudience.Youth, CapabilityReach.None))));
    }

    [Fact]
    public void Validate_RefusesALockedAudience()
    {
        var errors = KurinAccessPolicy.Validate(Matrix((KurinCapability.SeeContacts, PolicyAudience.Zvyazkovyi, CapabilityReach.OwnGroup)));

        Assert.Single(errors);
        Assert.Throws<ArgumentException>(() =>
            KurinAccessPolicy.FromMatrix(Matrix((KurinCapability.SeeContacts, PolicyAudience.Zvyazkovyi, CapabilityReach.OwnGroup))));
    }

    [Fact]
    public void Validate_RefusesTheYouthColumn_OnAnActionRow()
    {
        Assert.Single(KurinAccessPolicy.Validate(Matrix((KurinCapability.GiveWarning1, PolicyAudience.Youth, CapabilityReach.OwnGroup))));
        Assert.Empty(KurinAccessPolicy.Validate(Matrix((KurinCapability.GiveWarning1, PolicyAudience.Youth, CapabilityReach.None))));
    }

    [Theory]
    [InlineData("SeeFutureThing", "Youth", "None")]
    [InlineData("seeContacts", "Youth", "None")]
    [InlineData("0", "Youth", "None")]
    [InlineData("SeeContacts", "Martian", "None")]
    [InlineData("SeeContacts", "Youth", "Galaxy")]
    [InlineData("SeeContacts", "Youth", "kurin")]
    [InlineData("SeeContacts", "Youth", "2")]
    [InlineData("SeeContacts", "Youth", null)]
    public void Validate_RefusesNamesItDoesNotKnow(string capability, string audience, string? reach)
    {
        var matrix = new Dictionary<string, IReadOnlyDictionary<string, string?>>
        {
            [capability] = new Dictionary<string, string?> { [audience] = reach }
        };

        Assert.Single(KurinAccessPolicy.Validate(matrix));
    }
}
