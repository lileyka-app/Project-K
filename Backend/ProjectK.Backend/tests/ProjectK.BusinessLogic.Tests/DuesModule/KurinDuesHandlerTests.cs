using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Entry;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Receive;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class KurinDuesHandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid GroupA = Guid.NewGuid();

    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly List<DuesEntry> _entries = [];
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private string _upstream = KraiDues.Stanytsia;

    public KurinDuesHandlerTests()
    {
        var entries = new Mock<IDuesEntryRepository>();
        entries.Setup(r => r.Create(It.IsAny<DuesEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DuesEntry, CancellationToken>((e, _) => _entries.Add(e));
        entries.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _entries.FirstOrDefault(e => e.DuesEntryKey == key));
        _dues.SetupGet(d => d.DuesEntries).Returns(entries.Object);
        _user.SetupGet(u => u.UserId).Returns(Actor);
        _user.SetupGet(u => u.KurinKey).Returns(Kurin);
    }

    private KurinDuesAccess Access() => new(_dues.Object, _user.Object);

    private DuesEntryWriter Writer()
    {
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindKurinKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Kurin);
        return new DuesEntryWriter(_dues.Object, new Mock<IMembershipDirectory>().Object, members.Object, KraiProfiles.Paying(Kurin, _upstream));
    }

    private DuesEntry Transfer(decimal amount)
    {
        var transfer = new DuesEntry
        {
            KurinKey = Kurin, GroupKey = GroupA, Kind = DuesEntryKind.TransferToKurin,
            Method = DuesPaymentMethod.Cash, Amount = amount, OccurredOn = new DateOnly(2026, 10, 1)
        };
        _entries.Add(transfer);
        return transfer;
    }

    [Fact]
    public async Task ConfirmingATransfer_StampsWhoAndWhen_AndLeavesATrail()
    {
        var transfer = Transfer(255);
        var handler = new SetDuesTransferReceivedCommandHandler(Access(), _dues.Object, _user.Object, _time);

        var result = await handler.Handle(new SetDuesTransferReceivedCommand(Kurin, transfer.DuesEntryKey, true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        transfer.ReceivedAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime);
        transfer.ReceivedByUserKey.Should().Be(Actor);
        transfer.Events.Select(e => e.Action).Should().Equal(DuesEntryTrail.Received);

        await handler.Handle(new SetDuesTransferReceivedCommand(Kurin, transfer.DuesEntryKey, false), CancellationToken.None);

        transfer.ReceivedAtUtc.Should().BeNull();
        transfer.Events.Select(e => e.Action).Should().Equal(DuesEntryTrail.Received, DuesEntryTrail.Unreceived);
    }

    [Fact]
    public async Task OnlyATransferToThisKurin_CanBeConfirmed()
    {
        var expense = new DuesEntry { KurinKey = Kurin, GroupKey = GroupA, Kind = DuesEntryKind.Expense, Amount = 10 };
        var elsewhere = new DuesEntry { KurinKey = Guid.NewGuid(), GroupKey = GroupA, Kind = DuesEntryKind.TransferToKurin, Amount = 10 };
        _entries.AddRange([expense, elsewhere]);
        var handler = new SetDuesTransferReceivedCommandHandler(Access(), _dues.Object, _user.Object, _time);

        (await handler.Handle(new SetDuesTransferReceivedCommand(Kurin, expense.DuesEntryKey, true), CancellationToken.None))
            .Type.Should().Be(ResultType.NotFound);
        (await handler.Handle(new SetDuesTransferReceivedCommand(Kurin, elsewhere.DuesEntryKey, true), CancellationToken.None))
            .Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task AnotherKurinsBox_IsRefused()
    {
        var handler = new SetDuesTransferReceivedCommandHandler(Access(), _dues.Object, _user.Object, _time);

        var result = await handler.Handle(new SetDuesTransferReceivedCommand(Guid.NewGuid(), Transfer(1).DuesEntryKey, true), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task AKurinEntry_IsWrittenWithoutAGroup_AndVerifiedByTheZvyazkovyiLocks()
    {
        var request = new UpsertDuesEntryRequest
        {
            Kind = DuesEntryKind.TransferToStanytsia, Method = DuesPaymentMethod.Card, Amount = 240, OccurredOn = new DateOnly(2026, 10, 5)
        };

        var created = await new CreateKurinDuesEntryCommandHandler(Access(), Writer(), _dues.Object, _user.Object, _time)
            .Handle(new CreateKurinDuesEntryCommand(Kurin, request), CancellationToken.None);
        var entry = _entries.Single();
        await new SetKurinDuesEntryVerifiedCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new SetKurinDuesEntryVerifiedCommand(Kurin, entry.DuesEntryKey, true), CancellationToken.None);
        var delete = await new DeleteKurinDuesEntryCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new DeleteKurinDuesEntryCommand(Kurin, entry.DuesEntryKey), CancellationToken.None);

        created.Type.Should().Be(ResultType.Created);
        entry.GroupKey.Should().BeNull();
        entry.KurinKey.Should().Be(Kurin);
        delete.Type.Should().Be(ResultType.Conflict);
        entry.Events.Select(e => e.Action).Should().Equal(DuesEntryTrail.Created, DuesEntryTrail.Verified);
    }

    [Theory]
    [InlineData(KraiDues.Stanytsia, DuesEntryKind.TransferToKrai, false)]
    [InlineData(KraiDues.Krai, DuesEntryKind.TransferToStanytsia, false)]
    [InlineData(KraiDues.Krai, DuesEntryKind.TransferToKrai, true)]
    [InlineData(KraiDues.Both, DuesEntryKind.TransferToStanytsia, true)]
    [InlineData(KraiDues.Both, DuesEntryKind.TransferToKrai, true)]
    public async Task TheKurin_HandsMoneyOnOnlyTheWayItsKraiTakesIt(string upstream, DuesEntryKind kind, bool taken)
    {
        _upstream = upstream;
        var request = new UpsertDuesEntryRequest { Kind = kind, Method = DuesPaymentMethod.Card, Amount = 240, OccurredOn = new DateOnly(2026, 10, 5) };

        var result = await new CreateKurinDuesEntryCommandHandler(Access(), Writer(), _dues.Object, _user.Object, _time)
            .Handle(new CreateKurinDuesEntryCommand(Kurin, request), CancellationToken.None);

        result.Type.Should().Be(taken ? ResultType.Created : ResultType.BadRequest);
        _entries.Should().HaveCount(taken ? 1 : 0);
    }

    [Fact]
    public async Task AGroupsOperation_IsNotTheKurinsToEdit()
    {
        var groupExpense = new DuesEntry { KurinKey = Kurin, GroupKey = GroupA, Kind = DuesEntryKind.Expense, Amount = 10 };
        _entries.Add(groupExpense);

        var result = await new DeleteKurinDuesEntryCommandHandler(Access(), _dues.Object, _user.Object, _time)
            .Handle(new DeleteKurinDuesEntryCommand(Kurin, groupExpense.DuesEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        groupExpense.IsDeleted.Should().BeFalse();
    }
}
