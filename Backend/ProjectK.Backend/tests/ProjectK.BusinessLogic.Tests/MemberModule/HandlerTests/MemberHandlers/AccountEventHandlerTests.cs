using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MemberModule.Events;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Events.AuthModule;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Events.UsersModule;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule.HandlerTests.MemberHandlers;

/// <summary>What the member side does when an account is activated or changes its contacts.</summary>
public class AccountEventHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Guid _userKey = Guid.NewGuid();

    public AccountEventHandlerTests()
    {
        _unitOfWork.SetupGet(u => u.Members).Returns(_members.Object);
    }

    private Task Activate(Guid kurinKey) =>
        new AccountActivatedEventHandler(_unitOfWork.Object, _events.Object).Handle(
            new DomainEventNotification<AccountActivated>(new AccountActivated(
                new MemberForAccount(_userKey, "new@example.com", "Ліна", "Лідер", "+380501111111", new DateOnly(2010, 1, 1), kurinKey))),
            CancellationToken.None);

    [Fact]
    public async Task Activation_LinksThePersonWhoAlreadyCarriesTheAddress()
    {
        var existing = new Member { MemberKey = Guid.NewGuid(), Email = "new@example.com" };
        _members.Setup(m => m.GetByEmailAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Activate(Guid.Empty);

        existing.UserKey.Should().Be(_userKey);
        _members.Verify(m => m.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.PublishAsync(new MemberAccountLinked(existing.MemberKey, _userKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activation_OpensAPerson_AndPlacesThemOnlyWhenAKurinStandsBehindTheAccount()
    {
        Member? created = null;
        _members.Setup(m => m.Create(It.IsAny<Member>(), It.IsAny<CancellationToken>())).Callback<Member, CancellationToken>((m, _) => created = m);

        await Activate(Guid.Empty);

        created.Should().NotBeNull();
        created.UserKey.Should().Be(_userKey);
        _events.Verify(e => e.PublishAsync(It.IsAny<MemberPlaced>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ContactChanges_ReachTheLinkedPerson_AndCommit()
    {
        var member = new Member { MemberKey = Guid.NewGuid(), UserKey = _userKey };
        _members.Setup(m => m.GetTrackedByUserKeyAsync(_userKey, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        var handler = new AccountContactSyncEventHandler(_unitOfWork.Object);

        await handler.Handle(new DomainEventNotification<AccountEmailChanged>(new AccountEmailChanged(_userKey, "moved@example.com")), CancellationToken.None);
        await handler.Handle(new DomainEventNotification<AccountPhoneChanged>(new AccountPhoneChanged(_userKey, "+380502222222")), CancellationToken.None);

        member.Email.Should().Be("moved@example.com");
        member.PhoneNumber.Should().Be("+380502222222");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ContactChanges_WithoutALinkedPerson_DoNothing()
    {
        await new AccountContactSyncEventHandler(_unitOfWork.Object)
            .Handle(new DomainEventNotification<AccountEmailChanged>(new AccountEmailChanged(_userKey, "moved@example.com")), CancellationToken.None);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
