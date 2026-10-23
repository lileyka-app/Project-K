using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// What each audience sees of a person under a kurin's policy: of themselves, of someone in a гурток
/// they count as theirs, and of someone elsewhere — for every group of <see cref="MemberDataFields"/>.
/// </summary>
public class MemberFieldMaskTests
{
    private static readonly Guid OwnGroup = Guid.NewGuid();
    private static readonly Guid OtherGroup = Guid.NewGuid();
    private static readonly Guid Viewer = Guid.NewGuid();

    private static readonly Dictionary<PolicyAudience, string> RoleOf = new()
    {
        [PolicyAudience.Zvyazkovyi] = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi),
        [PolicyAudience.Vykhovnyk] = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk),
        [PolicyAudience.Instruktor] = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Instruktor),
        [PolicyAudience.Kurinnyy] = SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy),
        [PolicyAudience.KurinProvid] = SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Pysar),
        [PolicyAudience.Hurtkovyy] = SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy),
        [PolicyAudience.GroupProvid] = SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Pysar),
        [PolicyAudience.Youth] = SystemRole.Member
    };

    // The closed defaults the owner chose: contacts within one's own гурток for its провід and
    // юнаки, the year of birth for the КВ only.
    private static readonly KurinAccessPolicy Closed = KurinAccessPolicy.FromMatrix(new Dictionary<string, IReadOnlyDictionary<string, string?>>
    {
        [nameof(KurinCapability.SeeContacts)] = new Dictionary<string, string?>
        {
            [nameof(PolicyAudience.Hurtkovyy)] = nameof(CapabilityReach.OwnGroup),
            [nameof(PolicyAudience.GroupProvid)] = nameof(CapabilityReach.OwnGroup),
            [nameof(PolicyAudience.Youth)] = nameof(CapabilityReach.OwnGroup)
        },
        [nameof(KurinCapability.SeeBirthDate)] = new Dictionary<string, string?>
        {
            [nameof(PolicyAudience.Kurinnyy)] = nameof(CapabilityReach.None),
            [nameof(PolicyAudience.KurinProvid)] = nameof(CapabilityReach.None),
            [nameof(PolicyAudience.Hurtkovyy)] = nameof(CapabilityReach.None),
            [nameof(PolicyAudience.GroupProvid)] = nameof(CapabilityReach.None),
            [nameof(PolicyAudience.Youth)] = nameof(CapabilityReach.None)
        }
    });

    public static TheoryData<PolicyAudience, MemberDataGroup> EveryAudienceAndGroup()
    {
        var data = new TheoryData<PolicyAudience, MemberDataGroup>();
        foreach (var audience in KurinCapabilities.Audiences)
        {
            foreach (var info in MemberDataFields.All)
            {
                data.Add(audience, info.Group);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryAudienceAndGroup))]
    public void EveryAudience_ShouldSeeEachGroup_AsFarAsThePolicyReaches(PolicyAudience audience, MemberDataGroup group)
    {
        var mask = MaskFor(audience, Closed);
        var reach = ReachOf(audience, group, Closed);

        Assert.True(mask.Shows(group, Viewer, OtherGroup));
        Assert.Equal(reach != CapabilityReach.None, mask.Shows(group, Guid.NewGuid(), OwnGroup));
        Assert.Equal(reach == CapabilityReach.Kurin, mask.Shows(group, Guid.NewGuid(), OtherGroup));
    }

    [Fact]
    public void AYouth_UnderTheClosedPolicy_ShouldSeeHisGroupsContacts_AndOnlyTheBirthdayOfAnyone()
    {
        var mask = MaskFor(PolicyAudience.Youth, Closed);
        var mate = APerson();
        var stranger = APerson();

        mask.Apply(mate, Guid.NewGuid(), OwnGroup);
        mask.Apply(stranger, Guid.NewGuid(), OtherGroup);

        Assert.Equal("mate@example.com", mate.Email);
        Assert.Null(mate.DateOfBirth);
        Assert.Equal("05-17", mate.Birthday);
        Assert.Equal(["birthDate", "address", "parents", "extras"], mate.HiddenData);

        Assert.Empty(stranger.Email);
        Assert.Empty(stranger.PhoneNumber);
        Assert.Equal("05-17", stranger.Birthday);
        Assert.Equal(["contacts", "birthDate", "address", "parents", "extras"], stranger.HiddenData);
    }

    [Fact]
    public void Oneself_ShouldBeSeenWhole_WhateverThePolicy()
    {
        var mask = new MemberFieldMask(Viewer, new Dictionary<MemberDataGroup, CapabilityReach>(), []);
        var self = APerson();

        mask.Apply(self, Viewer, OtherGroup);

        Assert.Equal("mate@example.com", self.Email);
        Assert.Equal(new DateOnly(2010, 5, 17), self.DateOfBirth);
        Assert.Equal("Street 1", self.Address);
        Assert.Empty(self.HiddenData);
    }

    [Fact]
    public void TheBirthday_ShouldFollowTheBirthDate_WhenOnlyTheDateIsShown()
    {
        var mask = new MemberFieldMask(
            Viewer,
            new Dictionary<MemberDataGroup, CapabilityReach> { [MemberDataGroup.BirthDate] = CapabilityReach.Kurin },
            []);

        Assert.True(mask.Shows(MemberDataGroup.Birthday, Guid.NewGuid(), OtherGroup));
    }

    [Fact]
    public void TheBirthday_ShouldBeHidden_WhenNeitherIsShown()
    {
        var mask = new MemberFieldMask(Viewer, new Dictionary<MemberDataGroup, CapabilityReach>(), []);
        var person = APerson();

        mask.Apply(person, Guid.NewGuid(), OtherGroup);

        Assert.Null(person.Birthday);
        Assert.Contains("birthday", person.HiddenData);
    }

    [Fact]
    public void EveryDataGroup_ShouldBeShownByItsOwnCapabilityRow()
    {
        var dataRows = KurinCapabilities.All
            .Where(descriptor => descriptor.Group == CapabilityGroup.MemberData)
            .Select(descriptor => descriptor.Key);

        Assert.Equal(dataRows.Order(), MemberDataFields.All.Select(info => info.Capability).Order());
    }

    /// <summary>
    /// The kurin report prints everyone's data unmasked. That holds only while it is the Звʼязковий's
    /// and the admin's alone, and they see everything under any policy: the endpoint is gated on
    /// whole-kurin management, everyone who has that is the Звʼязковий's audience, and that column
    /// is locked to the whole kurin on every data row.
    /// </summary>
    [Fact]
    public void TheKurinReport_ShouldStayWithThoseTheMaskNeverHidesAnythingFrom()
    {
        var authorize = typeof(KurinController)
            .GetMethod(nameof(KurinController.ExportReportPdf))!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();
        Assert.Equal(AuthorizationPolicies.RequireKurinManagement, authorize.Policy);

        var managers = SystemRole.All().Where(role => RolePermissionMap.GrantsWholeKurinManagement([role])).ToList();
        Assert.NotEmpty(managers);
        Assert.All(managers, role => Assert.Contains(PolicyAudience.Zvyazkovyi, PolicyAudiences.For([role])));

        foreach (var info in MemberDataFields.All)
        {
            var descriptor = KurinCapabilities.Get(info.Capability);
            Assert.True(descriptor.IsLocked(PolicyAudience.Zvyazkovyi));
            Assert.Equal(CapabilityReach.Kurin, descriptor.Defaults[PolicyAudience.Zvyazkovyi]);
            Assert.NotEmpty(KurinAccessPolicy.Validate(new Dictionary<string, IReadOnlyDictionary<string, string?>>
            {
                [info.Capability.ToString()] = new Dictionary<string, string?>
                {
                    [nameof(PolicyAudience.Zvyazkovyi)] = nameof(CapabilityReach.OwnGroup)
                }
            }));
        }
    }

    private static CapabilityReach ReachOf(PolicyAudience audience, MemberDataGroup group, KurinAccessPolicy policy)
    {
        var audiences = PolicyAudiences.For([RoleOf[audience]]);
        var reach = policy.Reach(MemberDataFields.Of(group).Capability, audiences);
        if (group == MemberDataGroup.Birthday)
        {
            var dateReach = policy.Reach(KurinCapability.SeeBirthDate, audiences);
            reach = dateReach > reach ? dateReach : reach;
        }

        return reach;
    }

    private static MemberFieldMask MaskFor(PolicyAudience audience, KurinAccessPolicy policy)
    {
        var audiences = PolicyAudiences.For([RoleOf[audience]]);
        return new MemberFieldMask(
            Viewer,
            MemberDataFields.All.ToDictionary(info => info.Group, info => policy.Reach(info.Capability, audiences)),
            [OwnGroup]);
    }

    private static Person APerson() => new()
    {
        Email = "mate@example.com",
        PhoneNumber = "+380501234567",
        DateOfBirth = new DateOnly(2010, 5, 17),
        Address = "Street 1",
        School = "School 2",
        MotherPhone = "+380501112233",
        CardExtras = new Dictionary<string, string> { ["visaType"] = "D" }
    };

    private sealed class Person : IMemberPersonalData, IHasParentContacts, IHasCardExtras
    {
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
        public string? Birthday { get; set; }
        public string? Address { get; set; }
        public string? School { get; set; }
        public IReadOnlyList<string> HiddenData { get; set; } = [];
        public string? MotherFirstName { get; set; }
        public string? MotherLastName { get; set; }
        public string? MotherPhone { get; set; }
        public string? FatherFirstName { get; set; }
        public string? FatherLastName { get; set; }
        public string? FatherPhone { get; set; }
        public IDictionary<string, string> CardExtras { get; set; } = new Dictionary<string, string>();
    }
}
