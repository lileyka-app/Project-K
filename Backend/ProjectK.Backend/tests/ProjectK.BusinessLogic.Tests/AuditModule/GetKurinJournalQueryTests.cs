using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuditModule.Features.Journal.Get;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.AuditModule;

/// <summary>
/// Who reads how much of the journal, decided by the real policy: the КВ the whole kurin by
/// default, a впорядник his гуртки when the kurin narrows him, a юнак nothing.
/// </summary>
public class GetKurinJournalQueryTests
{
    private readonly PolicyAccess _access = new();
    private readonly Mock<IKurinJournalReader> _reader = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Guid _ownMemberKey = Guid.NewGuid();
    private KurinJournalFilter? _filter;
    private (int Skip, int Take) _page;

    public GetKurinJournalQueryTests()
    {
        _reader.Setup(x => x.ReadAsync(It.IsAny<KurinJournalFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<KurinJournalFilter, int, int, CancellationToken>((filter, skip, take, _) =>
            {
                _filter = filter;
                _page = (skip, take);
            })
            .ReturnsAsync([]);
        _members.Setup(x => x.FindByAccountAsync(_access.ActorUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_ownMemberKey, _access.ActorUserKey, _access.KurinKey, _access.GroupKey, "Марта", "Впорядниця", "m@example.com", null));
    }

    private Task<ServiceResult<Modules.AuditModule.Models.KurinJournalPageResponse>> ReadAsync(
        ICurrentUserContext user,
        GetKurinJournalQuery? query = null)
    {
        var scopes = new Mock<IResourceScopeReader>();
        scopes.Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _access.LedGroups.ToArray());

        var handler = new GetKurinJournalQueryHandler(user, _access.PermissionsFor(user), scopes.Object, _members.Object, _reader.Object);
        return handler.Handle(query ?? Query(), CancellationToken.None);
    }

    private GetKurinJournalQuery Query(int skip = 0, int take = 20) =>
        new(_access.KurinKey, null, null, null, null, null, skip, take);

    [Fact]
    public async Task TheKv_ReadsTheWholeKurin_ByDefault()
    {
        var result = await ReadAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        result.Type.Should().Be(ResultType.Success);
        _filter!.Visibility.Should().BeNull();
        _filter.KurinKey.Should().Be(_access.KurinKey);
    }

    [Fact]
    public async Task AVykhovnyk_TheKurinNarrowsToHisGroup_ReadsHisGroupsAndHimself()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.ReadJournal, PolicyAudience.Vykhovnyk, CapabilityReach.OwnGroup));

        var result = await ReadAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        result.Type.Should().Be(ResultType.Success);
        _filter!.Visibility.Should().NotBeNull();
        _filter.Visibility!.GroupKeys.Should().BeEquivalentTo([_access.GroupKey]);
        _filter.Visibility.OwnMemberKey.Should().Be(_ownMemberKey);
    }

    [Fact]
    public async Task AYouth_IsRefused()
    {
        var result = await ReadAsync(_access.Actor());

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("JournalReadNotAllowed");
        _reader.Verify(x => x.ReadAsync(It.IsAny<KurinJournalFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnotherKurinInTheRoute_IsRefused()
    {
        var result = await ReadAsync(
            _access.Actor(PolicyAccess.Zvyazkovyi),
            new GetKurinJournalQuery(Guid.NewGuid(), null, null, null, null, null, 0, 20));

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("JournalOutsideKurin");
    }

    [Fact]
    public async Task TheFilters_ReachTheRead_AsGiven()
    {
        var memberKey = Guid.NewGuid();
        var actorKey = Guid.NewGuid();
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

        await ReadAsync(
            _access.Actor(PolicyAccess.Zvyazkovyi),
            new GetKurinJournalQuery(_access.KurinKey, AuditArea.Award, memberKey, actorKey, from, to, 40, 20));

        _filter.Should().Be(new KurinJournalFilter(_access.KurinKey, AuditArea.Award, memberKey, actorKey, from, to, null));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(500, 50)]
    public async Task APage_IsAtMostFifty_AndAsksForOneMoreToKnowWhetherAnotherFollows(int asked, int expected)
    {
        await ReadAsync(_access.Actor(PolicyAccess.Zvyazkovyi), Query(take: asked));

        _page.Take.Should().Be(expected + 1);
    }

    [Fact]
    public async Task HasMore_IsTrue_OnlyWhenTheReadReturnedMoreThanAPage()
    {
        var entries = Enumerable.Range(0, 3)
            .Select(i => new KurinAuditEvent { Action = AuditActions.Award.Added, OccurredAtUtc = DateTime.UtcNow.AddMinutes(-i) })
            .ToList();
        _reader.Setup(x => x.ReadAsync(It.IsAny<KurinJournalFilter>(), 0, 3, It.IsAny<CancellationToken>())).ReturnsAsync(entries);
        _reader.Setup(x => x.ReadAsync(It.IsAny<KurinJournalFilter>(), 2, 3, It.IsAny<CancellationToken>())).ReturnsAsync(entries.Take(1).ToList());

        var first = await ReadAsync(_access.Actor(PolicyAccess.Zvyazkovyi), Query(skip: 0, take: 2));
        var last = await ReadAsync(_access.Actor(PolicyAccess.Zvyazkovyi), Query(skip: 2, take: 2));

        first.Data!.Items.Should().HaveCount(2);
        first.Data.HasMore.Should().BeTrue();
        last.Data!.Items.Should().HaveCount(1);
        last.Data.HasMore.Should().BeFalse();
    }
}
