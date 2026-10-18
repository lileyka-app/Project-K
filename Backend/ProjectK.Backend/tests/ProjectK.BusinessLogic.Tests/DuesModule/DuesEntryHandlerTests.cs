using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Create;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Delete;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Update;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Verify;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class DuesEntryHandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private readonly Kurin _kurin = new(7);
    private readonly Group _group;
    private readonly Group _otherGroup;
    private readonly KurinMembershipRecord _youth;
    private readonly KurinMembershipRecord _moved;

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly List<DuesEntry> _entries = [];
    private readonly List<DuesCharge> _charges = [];
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();

    public DuesEntryHandlerTests()
    {
        _group = new Group("Alpha", _kurin.KurinKey);
        _otherGroup = new Group("Bravo", _kurin.KurinKey);
        _youth = new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), _group.GroupKey, MembershipKind.Youth, DateTime.UtcNow, null);
        _moved = new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), _otherGroup.GroupKey, MembershipKind.Youth, DateTime.UtcNow, null);

        var entries = new Mock<IDuesEntryRepository>();
        entries.Setup(r => r.Create(It.IsAny<DuesEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DuesEntry, CancellationToken>((e, _) => _entries.Add(e));
        entries.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _entries.FirstOrDefault(e => e.DuesEntryKey == key));
        var charges = new Mock<IDuesChargeRepository>();
        charges.Setup(r => r.GetForKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => _charges);
        _dues.SetupGet(d => d.DuesEntries).Returns(entries.Object);
        _dues.SetupGet(d => d.DuesCharges).Returns(charges.Object);

        _user.SetupGet(u => u.UserId).Returns(Actor);
        _user.SetupGet(u => u.KurinKey).Returns(_kurin.KurinKey);
    }

    private GroupDuesAccess Access()
    {
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetByKeyAsync(_group.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_group);
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Groups).Returns(groups.Object);
        return new GroupDuesAccess(uow.Object, _user.Object);
    }

    private DuesEntryWriter Writer()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetInKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_youth, _moved]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindKurinKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(_kurin.KurinKey);
        return new DuesEntryWriter(_dues.Object, memberships.Object, members.Object, Mock.Of<ProjectK.Common.Interfaces.Modules.KraiModule.IKraiProfileProvider>());
    }

    private static UpsertDuesEntryRequest Contribution(Guid membershipKey, decimal amount = 300) => new()
    {
        Kind = DuesEntryKind.Contribution,
        Method = DuesPaymentMethod.Cash,
        Amount = amount,
        OccurredOn = new DateOnly(2026, 5, 1),
        MembershipKey = membershipKey,
        Note = "  Q2  "
    };

    private Task<ServiceResult<Guid>> CreateAsync(UpsertDuesEntryRequest request) =>
        new CreateDuesEntryCommandHandler(Access(), Writer(), _dues.Object, _user.Object, _time)
            .Handle(new CreateDuesEntryCommand(_group.GroupKey, request), CancellationToken.None);

    [Fact]
    public async Task Create_WritesTheEntryAndItsFirstTrailEvent()
    {
        var result = await CreateAsync(Contribution(_youth.MembershipKey));

        result.Type.Should().Be(ResultType.Created);
        var entry = _entries.Single();
        entry.GroupKey.Should().Be(_group.GroupKey);
        entry.KurinKey.Should().Be(_kurin.KurinKey);
        entry.Note.Should().Be("Q2");
        entry.CreatedByUserKey.Should().Be(Actor);
        entry.Events.Should().ContainSingle(e => e.Action == DuesEntryTrail.Created && e.ActorUserKey == Actor);
        entry.Events.Single().Snapshot.Should().Contain("300");
    }

    [Fact]
    public async Task Create_RefusesSomeoneWithNoAccountHere()
    {
        var result = await CreateAsync(Contribution(_moved.MembershipKey));

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("NotOfThisGroup");
        _entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AcceptsAPersonWhoMovedAwayButWasChargedHere()
    {
        _charges.Add(new DuesCharge { KurinKey = _kurin.KurinKey, GroupKey = _group.GroupKey, MembershipKey = _moved.MembershipKey, Quarter = 8100 });

        var result = await CreateAsync(Contribution(_moved.MembershipKey));

        result.Type.Should().Be(ResultType.Created);
    }

    [Fact]
    public async Task Update_RewritesAnUnverifiedEntryAndLeavesATrail()
    {
        await CreateAsync(Contribution(_youth.MembershipKey));
        var key = _entries.Single().DuesEntryKey;

        var result = await new UpdateDuesEntryCommandHandler(Access(), Writer(), _dues.Object, _user.Object, _time)
            .Handle(new UpdateDuesEntryCommand(_group.GroupKey, key, Contribution(_youth.MembershipKey, 150)), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _entries.Single().Amount.Should().Be(150);
        _entries.Single().Events.Select(e => e.Action).Should().Equal(DuesEntryTrail.Created, DuesEntryTrail.Updated);
    }

    [Fact]
    public async Task Verified_LocksTheEntryAgainstEditingAndDeleting()
    {
        await CreateAsync(Contribution(_youth.MembershipKey));
        var key = _entries.Single().DuesEntryKey;

        var verified = await new SetDuesEntryVerifiedCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new SetDuesEntryVerifiedCommand(_group.GroupKey, key, true), CancellationToken.None);
        var update = await new UpdateDuesEntryCommandHandler(Access(), Writer(), _dues.Object, _user.Object, _time)
            .Handle(new UpdateDuesEntryCommand(_group.GroupKey, key, Contribution(_youth.MembershipKey, 1)), CancellationToken.None);
        var delete = await new DeleteDuesEntryCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new DeleteDuesEntryCommand(_group.GroupKey, key), CancellationToken.None);

        verified.Type.Should().Be(ResultType.Success);
        _entries.Single().VerifiedByUserKey.Should().Be(Actor);
        update.Type.Should().Be(ResultType.Conflict);
        delete.Type.Should().Be(ResultType.Conflict);
        _entries.Single().Amount.Should().Be(300);
        _entries.Single().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Unverifying_OpensTheEntryAgain()
    {
        await CreateAsync(Contribution(_youth.MembershipKey));
        var key = _entries.Single().DuesEntryKey;
        var verify = new SetDuesEntryVerifiedCommandHandler(Access(), _dues.Object, _user.Object, _time);

        await verify.Handle(new SetDuesEntryVerifiedCommand(_group.GroupKey, key, true), CancellationToken.None);
        await verify.Handle(new SetDuesEntryVerifiedCommand(_group.GroupKey, key, false), CancellationToken.None);
        var delete = await new DeleteDuesEntryCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new DeleteDuesEntryCommand(_group.GroupKey, key), CancellationToken.None);

        delete.Type.Should().Be(ResultType.Success);
        var entry = _entries.Single();
        entry.IsDeleted.Should().BeTrue();
        entry.DeletedByUserKey.Should().Be(Actor);
        entry.Events.Select(e => e.Action).Should().Equal(
            DuesEntryTrail.Created, DuesEntryTrail.Verified, DuesEntryTrail.Unverified, DuesEntryTrail.Deleted);
    }

    [Fact]
    public async Task AnotherGroupsEntry_IsNotFoundHere()
    {
        _entries.Add(new DuesEntry { KurinKey = _kurin.KurinKey, GroupKey = _otherGroup.GroupKey, Kind = DuesEntryKind.Expense, Amount = 10 });

        var result = await new DeleteDuesEntryCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new DeleteDuesEntryCommand(_group.GroupKey, _entries.Single().DuesEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Theory]
    [InlineData(DuesEntryKind.Contribution, 0, null, false)]
    [InlineData(DuesEntryKind.Contribution, -5, null, false)]
    [InlineData(DuesEntryKind.Correction, -5, null, true)]
    [InlineData(DuesEntryKind.Exchange, 100, null, false)]
    [InlineData(DuesEntryKind.Exchange, 100, DuesPaymentMethod.Cash, false)]
    [InlineData(DuesEntryKind.Exchange, 100, DuesPaymentMethod.Card, true)]
    [InlineData(DuesEntryKind.TransferToStanytsia, 100, null, false)]
    public void Rules_HoldTheLine(DuesEntryKind kind, decimal amount, DuesPaymentMethod? counter, bool valid)
    {
        var request = new UpsertDuesEntryRequest
        {
            Kind = kind,
            Method = DuesPaymentMethod.Cash,
            CounterMethod = counter,
            Amount = amount,
            OccurredOn = new DateOnly(2026, 5, 1),
            MembershipKey = DuesEntryRules.PersonalKinds.Contains(kind) ? Guid.NewGuid() : null
        };

        new UpsertDuesEntryRequestValidator(_time).Validate(request).IsValid.Should().Be(valid);
    }

    // The kurin's box takes no personal operations and no transfers to itself; the станиця is its own to hand to.
    [Theory]
    [InlineData(DuesEntryKind.TransferToStanytsia, true)]
    [InlineData(DuesEntryKind.Expense, true)]
    [InlineData(DuesEntryKind.Contribution, false)]
    [InlineData(DuesEntryKind.TransferToKurin, false)]
    public void KurinRules_HoldTheirOwnLine(DuesEntryKind kind, bool valid)
    {
        var request = new UpsertDuesEntryRequest
        {
            Kind = kind,
            Method = DuesPaymentMethod.Cash,
            Amount = 100,
            OccurredOn = new DateOnly(2026, 5, 1),
            MembershipKey = DuesEntryRules.PersonalKinds.Contains(kind) ? Guid.NewGuid() : null
        };

        new UpsertKurinDuesEntryRequestValidator(_time).Validate(request).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Rules_RefuseADateInTheFutureAndAPersonOnAnExpense()
    {
        var validator = new UpsertDuesEntryRequestValidator(_time);

        validator.Validate(new UpsertDuesEntryRequest
        {
            Kind = DuesEntryKind.Expense, Method = DuesPaymentMethod.Cash, Amount = 10, OccurredOn = new DateOnly(2026, 6, 1)
        }).IsValid.Should().BeFalse();

        validator.Validate(new UpsertDuesEntryRequest
        {
            Kind = DuesEntryKind.Expense, Method = DuesPaymentMethod.Cash, Amount = 10, OccurredOn = new DateOnly(2026, 5, 1), MembershipKey = Guid.NewGuid()
        }).IsValid.Should().BeFalse();
    }
}
