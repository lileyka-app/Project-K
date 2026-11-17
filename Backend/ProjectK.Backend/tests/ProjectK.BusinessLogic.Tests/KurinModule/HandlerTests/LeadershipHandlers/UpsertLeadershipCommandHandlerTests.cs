using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.LeadershipHandlers;

public class UpsertLeadershipHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILeadershipRepository> _leadershipRepoMock = new();
    private readonly Mock<IKurinRepository> _kurinsMock = new();
    private readonly Mock<IMemberDirectory> _membersMock = new();
    private readonly Mock<IBackendCache> _cacheMock = new();
    private readonly Mock<IAccessChangeStore> _accessChangesMock = new();
    private readonly Mock<ProjectK.Common.Contracts.InfrastructureModule.ICurrentUserContext> _currentUserContextMock = new();
    private readonly UpsertLeadershipCommandHandler _handler;

    public UpsertLeadershipHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Leaderships).Returns(_leadershipRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Kurins).Returns(_kurinsMock.Object);
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[]
        {
            ProjectK.Common.Rules.IdentityModule.SystemRoleNames.ForOffice(
                ProjectK.Common.Enums.KurinModule.LeadershipType.KV,
                ProjectK.Common.Enums.KurinModule.LeadershipRole.Zvyazkovyi)
        });
        _membersMock
            .Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => keys
                .Select(key => new MemberSummary(key, AccountOf(key), Guid.NewGuid(), null, _firstNames.GetValueOrDefault(key, "A"), "B", "a@example.com", null))
                .ToList());
        _membersMock
            .Setup(m => m.GetLookupsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => keys.ToDictionary(key => key, key => new MemberLookupDto { MemberKey = key }));
        _journal = new JournalRecorder(_currentUserContextMock.Object);
        _handler = new UpsertLeadershipCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserContextMock.Object,
            _membersMock.Object,
            _cacheMock.Object,
            _accessChangesMock.Object,
            _journal.Audit);
    }

    private readonly JournalRecorder _journal;
    private readonly Dictionary<Guid, Guid> _accounts = new();
    private readonly Dictionary<Guid, string> _firstNames = new();

    private Guid AccountOf(Guid memberKey)
    {
        if (!_accounts.TryGetValue(memberKey, out var userKey))
        {
            userKey = Guid.NewGuid();
            _accounts[memberKey] = userKey;
        }

        return userKey;
    }

    private static UpsertLeadershipRequest BuildRequest(string type = "kurin") => new()
    {
        Type = type,
        EntityKey = Guid.NewGuid(),
        StartDate = new DateOnly(2024, 1, 1),
        EndDate = null,
        LeadershipHistories = new List<LeadershipHistoryMemberDto>()
    };

    private static Leadership BuildLeadershipEntity(Guid? key = null) => new()
    {
        LeadershipKey = key ?? Guid.NewGuid(),
        StartDate = new DateOnly(2024, 1, 1)
    };

    [Fact]
    public async Task Handle_ShouldCreateLeadership_WhenNoExistingFound()
    {
        var requestDto = BuildRequest("kurin");
        var command = new UpsertLeadershipCommand(requestDto);
        Leadership? added = null;
        _leadershipRepoMock
            .Setup(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()))
            .Callback((Leadership leadership, CancellationToken _) => added = leadership);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Leadership?)null);


        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.NotNull(result.Data);
        Assert.NotNull(added);
        Assert.Equal(added.LeadershipKey, result.Data.LeadershipKey);
        Assert.Equal("GetLeadershipByKey", result.CreatedAtActionName);
        Assert.NotNull(result.CreatedAtRouteValues);

        _leadershipRepoMock.Verify(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Once);
        _leadershipRepoMock.Verify(r => r.Update(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateLeadership_WhenProvidedKeyNotFound()
    {
        var providedKey = Guid.NewGuid();
        var requestDto = BuildRequest("group");
        var command = new UpsertLeadershipCommand(requestDto, providedKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(providedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Leadership?)null);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.Equal(providedKey, result.Data!.LeadershipKey);
        _leadershipRepoMock.Verify(r => r.Add(It.Is<Leadership>(l => l.LeadershipKey == providedKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAssignKurinKey_OnKurinTypeCreation()
    {
        var requestDto = BuildRequest("kurin");
        var command = new UpsertLeadershipCommand(requestDto);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.Equal(LeadershipType.Kurin, result.Data!.Type);
        Assert.Equal(requestDto.EntityKey, result.Data.KurinKey);
        Assert.Null(result.Data.GroupKey);
    }

    [Fact]
    public async Task Handle_ShouldAssignGroupKey_OnGroupTypeCreation()
    {
        var requestDto = BuildRequest("group");
        var command = new UpsertLeadershipCommand(requestDto);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.Equal(LeadershipType.Group, result.Data!.Type);
        Assert.Equal(requestDto.EntityKey, result.Data.GroupKey);
        Assert.Null(result.Data.KurinKey);
    }

    [Fact]
    public async Task Handle_ShouldAssignKurinKey_OnKvTypeCreation()
    {
        var requestDto = BuildRequest("kv"); // KV behaves like Kurin for key assignment
        var command = new UpsertLeadershipCommand(requestDto);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.Equal(LeadershipType.KV, result.Data!.Type);
        Assert.Equal(requestDto.EntityKey, result.Data.KurinKey);
        Assert.Null(result.Data.GroupKey);
    }

    [Fact]
    public async Task Handle_ShouldUpdateLeadership_WhenExistingFound()
    {
        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Group;
        existing.GroupKey = Guid.NewGuid();

        var requestDto = BuildRequest("kurin"); // change type
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(existing.LeadershipKey, result.Data!.LeadershipKey);
        Assert.Equal(LeadershipType.Kurin, result.Data.Type);
        Assert.Equal(requestDto.EntityKey, result.Data.KurinKey);
        Assert.Null(result.Data.GroupKey);

        _leadershipRepoMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
        _leadershipRepoMock.Verify(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SeatedBySystem_ShouldSucceedWithoutAnAssigner()
    {
        // Account activation seats the new kurin leader while the caller is still anonymous.
        _currentUserContextMock.Setup(x => x.Roles).Returns(Array.Empty<string>());

        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.KV;
        existing.KurinKey = Guid.NewGuid();

        var requestDto = BuildRequest("kv");
        requestDto.EntityKey = existing.KurinKey;
        requestDto.LeadershipHistories = new List<LeadershipHistoryMemberDto>
        {
            new()
            {
                Role = LeadershipRole.Zvyazkovyi.ToString(),
                Member = new MemberLookupDto
                {
                    MemberKey = Guid.NewGuid(),
                    FirstName = "New",
                    LastName = "Leader"
                }
            }
        };
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey) { SeatedBySystem = true };

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Contains(existing.LeadershipHistories, h => h.Role == LeadershipRole.Zvyazkovyi && h.EndDate == null);
    }

    [Theory]
    [InlineData("Kurin.Pysar", ResultType.Forbidden)]
    [InlineData("Kurin.Kurinnuy", ResultType.Success)]
    public async Task Handle_SeatingSkarbnyk_ShouldDependOnWhetherTheCallerHeadsTheProvid(
        string callerRole,
        ResultType expected)
    {
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { callerRole });

        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Kurin;
        existing.KurinKey = Guid.NewGuid();

        var requestDto = BuildRequest("kurin");
        requestDto.EntityKey = existing.KurinKey;
        requestDto.LeadershipHistories = new List<LeadershipHistoryMemberDto>
        {
            new()
            {
                Role = LeadershipRole.Skarbnyk.ToString(),
                Member = new MemberLookupDto
                {
                    MemberKey = Guid.NewGuid(),
                    FirstName = "Seated",
                    LastName = "Member"
                }
            }
        };
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expected, result.Type);
    }

    [Theory]
    [InlineData("KV.Vykhovnyk", LeadershipRole.Hurtkoviy, ResultType.Success)]
    [InlineData("KV.Vykhovnyk", LeadershipRole.Skarbnyk, ResultType.Success)]
    [InlineData("Group.Hurtkoviy", LeadershipRole.Hurtkoviy, ResultType.Forbidden)]
    [InlineData("Group.Pysar", LeadershipRole.Hurtkoviy, ResultType.Forbidden)]
    public async Task Handle_SeatingAGroupProvid_ShouldBeOpenToTheVporyadnyk(
        string callerRole,
        LeadershipRole seated,
        ResultType expected)
    {
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[] { callerRole });

        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Group;
        existing.GroupKey = Guid.NewGuid();

        var requestDto = BuildRequest("group");
        requestDto.EntityKey = existing.GroupKey;
        requestDto.LeadershipHistories = new List<LeadershipHistoryMemberDto>
        {
            new()
            {
                Role = seated.ToString(),
                Member = new MemberLookupDto { MemberKey = Guid.NewGuid(), FirstName = "Seated", LastName = "Member" }
            }
        };
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public async Task Handle_ShouldCloseOldActiveHistoryAndStartNewOne_WhenRoleMemberChanges()
    {
        var oldMemberKey = Guid.NewGuid();
        var newMemberKey = Guid.NewGuid();
        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Kurin;
        existing.KurinKey = Guid.NewGuid();
        existing.LeadershipHistories.Add(new LeadershipHistory
        {
            LeadershipHistoryKey = Guid.NewGuid(),
            LeadershipKey = existing.LeadershipKey,
            Leadership = existing,
            MemberKey = oldMemberKey,
            Role = LeadershipRole.Kurinnuy,
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = null
        });

        var requestDto = BuildRequest("kurin");
        requestDto.EntityKey = existing.KurinKey;
        requestDto.LeadershipHistories = new List<LeadershipHistoryMemberDto>
        {
            new()
            {
                Role = LeadershipRole.Kurinnuy.ToString(),
                Member = new MemberLookupDto
                {
                    MemberKey = newMemberKey,
                    FirstName = "New",
                    LastName = "Member"
                }
            }
        };
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await _handler.Handle(command, CancellationToken.None);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(today, existing.LeadershipHistories.Single(h => h.MemberKey == oldMemberKey).EndDate);

        var newHistory = Assert.Single(existing.LeadershipHistories, h => h.MemberKey == newMemberKey);
        Assert.Equal(LeadershipRole.Kurinnuy, newHistory.Role);
        Assert.Equal(today, newHistory.StartDate);
        Assert.Null(newHistory.EndDate);
    }

    [Fact]
    public async Task Handle_ShouldMarkTheOldAndNewHolders_AndDropTheLedGroupCache_WhenAHolderChanges()
    {
        var oldMemberKey = Guid.NewGuid();
        var newMemberKey = Guid.NewGuid();
        var stayingMemberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy(
            (LeadershipRole.Kurinnuy, oldMemberKey),
            (LeadershipRole.Pysar, stayingMemberKey));
        var command = new UpsertLeadershipCommand(
            RequestSeating(existing, (LeadershipRole.Kurinnuy, newMemberKey), (LeadershipRole.Pysar, stayingMemberKey)),
            existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(oldMemberKey)), Times.Once);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(newMemberKey)), Times.Once);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(stayingMemberKey)), Times.Never);
        _cacheMock.Verify(c => c.Invalidate(BackendCacheRules.MentorScopeReads), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheRoleThatChangedHands_WithWhoHeldItAndWhoHoldsItNow()
    {
        var oldMemberKey = Guid.NewGuid();
        var newMemberKey = Guid.NewGuid();
        var stayingMemberKey = Guid.NewGuid();
        _firstNames[oldMemberKey] = "Тарас";
        _firstNames[newMemberKey] = "Ореста";
        var existing = LeadershipHeldBy(
            (LeadershipRole.Kurinnuy, oldMemberKey),
            (LeadershipRole.Pysar, stayingMemberKey));
        var command = new UpsertLeadershipCommand(
            RequestSeating(existing, (LeadershipRole.Kurinnuy, newMemberKey), (LeadershipRole.Pysar, stayingMemberKey)),
            existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await _handler.Handle(command, CancellationToken.None);

        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditArea.Office, entry.Area);
        Assert.Equal(AuditActionNames.Office.Assigned, entry.Action);
        Assert.Equal(existing.KurinKey, entry.KurinKey);
        Assert.Equal(existing.LeadershipKey, entry.SubjectKey);
        Assert.Equal("Курінний", entry.SubjectLabel);
        Assert.Equal(newMemberKey, entry.SubjectMemberKey);
        Assert.Equal(new[] { new AuditChange("holder", "Тарас B", "Ореста B") }, entry.Changes);
    }

    [Fact]
    public async Task Handle_ShouldRecordARoleLeftEmpty_AboutTheOneWhoLeftIt()
    {
        var memberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy((LeadershipRole.Pysar, memberKey));
        var command = new UpsertLeadershipCommand(RequestSeating(existing), existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditActionNames.Office.Ended, entry.Action);
        Assert.Equal("Писар", entry.SubjectLabel);
        Assert.Equal(memberKey, entry.SubjectMemberKey);
        Assert.Equal(new[] { new AuditChange("holder", "A B", null) }, entry.Changes);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheRole_AsTheKurinNamesIt()
    {
        var memberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy((LeadershipRole.Pysar, memberKey));
        _kurinsMock.Setup(k => k.GetByKeyAsync(existing.KurinKey!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(5, KurinBranch.USP) { KurinKey = existing.KurinKey!.Value, RoleNames = """{"Pysar":"Скриптор"}""" });
        var command = new UpsertLeadershipCommand(RequestSeating(existing), existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal("Скриптор", Assert.Single(_journal.Entries).SubjectLabel);
    }

    [Fact]
    public async Task Handle_ShouldMarkNobody_WhenTheSameHoldersAreResubmitted()
    {
        var memberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy((LeadershipRole.Kurinnuy, memberKey));
        var command = new UpsertLeadershipCommand(
            RequestSeating(existing, (LeadershipRole.Kurinnuy, memberKey)),
            existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        _accessChangesMock.Verify(s => s.MarkUser(It.IsAny<Guid>()), Times.Never);
        _cacheMock.Verify(c => c.Invalidate(It.IsAny<CachePolicy>()), Times.Never);
        Assert.Empty(_journal.Entries);
    }

    [Fact]
    public async Task Handle_ShouldMarkEverySittingHolder_WhenTheProvidMovesToAnotherKurin()
    {
        var memberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy((LeadershipRole.Kurinnuy, memberKey));
        var request = RequestSeating(existing, (LeadershipRole.Kurinnuy, memberKey));
        request.EntityKey = Guid.NewGuid();
        var command = new UpsertLeadershipCommand(request, existing.LeadershipKey);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(memberKey)), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCacheRules.MentorScopeReads), Times.Once);
        Assert.Empty(_journal.Entries);
    }

    private Leadership LeadershipHeldBy(params (LeadershipRole Role, Guid MemberKey)[] holders)
    {
        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Kurin;
        existing.KurinKey = Guid.NewGuid();
        foreach (var (role, memberKey) in holders)
        {
            existing.LeadershipHistories.Add(new LeadershipHistory
            {
                LeadershipHistoryKey = Guid.NewGuid(),
                LeadershipKey = existing.LeadershipKey,
                Leadership = existing,
                MemberKey = memberKey,
                Role = role,
                StartDate = new DateOnly(2024, 1, 1),
                EndDate = null
            });
        }

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        return existing;
    }

    private static UpsertLeadershipRequest RequestSeating(Leadership existing, params (LeadershipRole Role, Guid MemberKey)[] holders)
    {
        var request = BuildRequest("kurin");
        request.EntityKey = existing.KurinKey;
        request.LeadershipHistories = holders
            .Select(holder => new LeadershipHistoryMemberDto
            {
                Role = holder.Role.ToString(),
                Member = new MemberLookupDto { MemberKey = holder.MemberKey, FirstName = "A", LastName = "B" }
            })
            .ToList();
        return request;
    }

    [Fact]
    public async Task Handle_ShouldReturnInternalServerError_WhenNoChangesPersisted_OnCreate()
    {
        var requestDto = BuildRequest("kurin");
        var command = new UpsertLeadershipCommand(requestDto);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.InternalServerError, result.Type);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldReturnInternalServerError_WhenNoChangesPersisted_OnUpdate()
    {
        var existing = BuildLeadershipEntity();
        existing.Type = LeadershipType.Kurin;
        existing.KurinKey = Guid.NewGuid();

        var requestDto = BuildRequest("group");
        var command = new UpsertLeadershipCommand(requestDto, existing.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(existing.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.InternalServerError, result.Type);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenInvalidType()
    {
        var requestDto = BuildRequest("INVALID_TYPE");
        var command = new UpsertLeadershipCommand(requestDto);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldTreatGuidEmptyLeadershipKey_AsCreate()
    {
        var requestDto = BuildRequest("group");
        // Mimic constructing with an empty leadership key (edge case)
        var command = new UpsertLeadershipCommand(requestDto, Guid.Empty);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        _leadershipRepoMock.Verify(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Once);
        _leadershipRepoMock.Verify(r => r.Update(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A new провід seats the people the request names and nothing else of the request reaches it:
    /// the holders get keys of their own, start today, and a closed row in the request is not seated.
    /// </summary>
    [Fact]
    public async Task Handle_OnCreate_ShouldSeatTheRequestedHolders_AndTakeNothingElseFromTheirRows()
    {
        var seatedKey = Guid.NewGuid();
        var requestedHistoryKey = Guid.NewGuid();
        var requestDto = BuildRequest("kurin");
        requestDto.LeadershipHistories = new List<LeadershipHistoryMemberDto>
        {
            new()
            {
                LeadershipHistoryKey = requestedHistoryKey,
                Role = LeadershipRole.Pysar.ToString(),
                StartDate = new DateOnly(2001, 1, 1),
                Member = new MemberLookupDto { MemberKey = seatedKey, FirstName = "Seated", LastName = "Member" }
            },
            new()
            {
                Role = LeadershipRole.Skarbnyk.ToString(),
                EndDate = new DateOnly(2002, 1, 1),
                Member = new MemberLookupDto { MemberKey = Guid.NewGuid(), FirstName = "Gone", LastName = "Member" }
            }
        };
        Leadership? added = null;
        _leadershipRepoMock
            .Setup(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()))
            .Callback((Leadership leadership, CancellationToken _) => added = leadership);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertLeadershipCommand(requestDto), CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        var holder = Assert.Single(added!.LeadershipHistories);
        Assert.Equal(seatedKey, holder.MemberKey);
        Assert.Equal(LeadershipRole.Pysar, holder.Role);
        Assert.NotEqual(requestedHistoryKey, holder.LeadershipHistoryKey);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), holder.StartDate);
        Assert.Null(holder.EndDate);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), added.StartDate);
        Assert.Equal(requestDto.EntityKey, added.KurinKey);
    }
}
