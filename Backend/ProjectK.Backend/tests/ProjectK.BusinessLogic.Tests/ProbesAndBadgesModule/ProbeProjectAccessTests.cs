using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

/// <summary>
/// The person writes their own projects; besides them, whoever the kurin lets sign their probe.
/// The probe is found by its place in the person's line, never by its id.
/// </summary>
public class ProbeProjectAccessTests
{
    private readonly Mock<IMemberProbeLine> _line = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly Mock<IResourceAccessService> _rights = new();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly ProbeProjectAccess _access;

    public ProbeProjectAccessTests()
    {
        _user.SetupGet(u => u.UserId).Returns(_userKey);
        _rights.Setup(r => r.CheckCapabilityAsync(KurinCapability.SignProbe, ResourceType.Member, _memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("no"));
        _line.Setup(l => l.ForMemberAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ProbeSummaryDto("upyu-2026-0", "Прихильник", 3, 1, "upyu-2026", 0, "", ""),
            new ProbeSummaryDto("upyu-2026-1", "Перша", 10, 2, "upyu-2026", 1, "", ""),
            new ProbeSummaryDto("upyu-2026-2", "Друга", 10, 2, "upyu-2026", 2, "", ""),
            new ProbeSummaryDto("upyu-2026-3", "Третя", 0, 0, "upyu-2026", 3, "", "")
        ]);
        _access = new ProbeProjectAccess(_line.Object, _members.Object, _user.Object, _rights.Object);
    }

    [Theory]
    [InlineData("upyu-2026-3", true)]
    [InlineData("upyu-2026-2", false)]
    [InlineData("old-3", false)]
    public async Task OnlyTheProbeAtTheThirdPlace_HasProjects(string probeId, bool expected)
    {
        (await _access.IsProjectProbeAsync(_memberKey, probeId)).Should().Be(expected);
    }

    [Fact]
    public async Task ThePersonThemself_MayEdit()
    {
        _members.Setup(m => m.FindAccountKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_userKey);

        (await _access.CanEditAsync(_memberKey)).Should().BeTrue();
    }

    [Fact]
    public async Task WhoeverMaySignTheProbe_MayEdit()
    {
        _rights.Setup(r => r.CheckCapabilityAsync(KurinCapability.SignProbe, ResourceType.Member, _memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        (await _access.CanEditAsync(_memberKey)).Should().BeTrue();
    }

    [Fact]
    public async Task ALeaderOnTheirOwnThirdProbe_WritesIt()
    {
        _members.Setup(m => m.FindAccountKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_userKey);

        (await _access.CanEditAsync(_memberKey)).Should().BeTrue();
    }

    [Fact]
    public async Task SomeoneElse_MayNot()
    {
        _members.Setup(m => m.FindAccountKeyAsync(_memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        (await _access.CanEditAsync(_memberKey)).Should().BeFalse();
    }
}
