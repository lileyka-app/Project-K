using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.LeadershipHandlers;

public class UpsertLeadershipHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILeadershipRepository> _leadershipRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IMemberDirectory> _membersMock = new();
    private readonly Mock<IBackendCache> _cacheMock = new();
    private readonly Mock<IAccessChangeSignal> _accessChangesMock = new();
    private readonly Mock<ProjectK.Common.Interfaces.Modules.InfrastructureModule.ICurrentUserContext> _currentUserContextMock = new();
    private readonly UpsertLeadershipCommandHandler _handler;

    public UpsertLeadershipHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Leaderships).Returns(_leadershipRepoMock.Object);
        _currentUserContextMock.Setup(x => x.Roles).Returns(new[]
        {
            ProjectK.Common.Models.Authorization.SystemRole.ForOffice(
                ProjectK.Common.Models.Enums.LeadershipType.KV,
                ProjectK.Common.Models.Enums.LeadershipRole.Zvyazkovyi)
        });
        _membersMock
            .Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => keys
                .Select(key => new MemberSummary(key, AccountOf(key), Guid.NewGuid(), null, _firstNames.GetValueOrDefault(key, "A"), "B", "a@example.com", null))
                .ToList());
        _journal = new JournalRecorder(_currentUserContextMock.Object);
        _handler = new UpsertLeadershipCommandHandler(
            _unitOfWorkMock.Object,
            _mapperMock.Object,
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

    private void SetupCreateMapping(UpsertLeadershipCommand command, Leadership entity)
    {
        _mapperMock
            .Setup(m => m.Map<Leadership>(command))
            .Returns(() =>
            {
                // emulate mapping from command to new entity
                entity.LeadershipKey = command.LeadershipKey ?? entity.LeadershipKey;
                entity.StartDate = command.StartDate;
                entity.EndDate = command.EndDate;
                return entity;
            });

        _mapperMock
            .Setup(m => m.Map<LeadershipResponse>(It.IsAny<Leadership>()))
            .Returns((Leadership src) => new LeadershipResponse
            {
                LeadershipKey = src.LeadershipKey,
                Type = src.Type,
                KurinKey = src.KurinKey,
                GroupKey = src.GroupKey,
                StartDate = src.StartDate,
                EndDate = src.EndDate,
                LeadershipHistories = new List<LeadershipHistoryMemberDto>()
            });
    }

    private void SetupUpdateMapping(UpsertLeadershipCommand command, Leadership existing)
    {
        _mapperMock
            .Setup(m => m.Map(command, existing))
            .Returns(() =>
            {
                existing.StartDate = command.StartDate;
                existing.EndDate = command.EndDate;
                return existing;
            });

        _mapperMock
            .Setup(m => m.Map<LeadershipResponse>(It.IsAny<Leadership>()))
            .Returns((Leadership src) => new LeadershipResponse
            {
                LeadershipKey = src.LeadershipKey,
                Type = src.Type,
                KurinKey = src.KurinKey,
                GroupKey = src.GroupKey,
                StartDate = src.StartDate,
                EndDate = src.EndDate,
                LeadershipHistories = new List<LeadershipHistoryMemberDto>()
            });
    }

    [Fact]
    public async Task Handle_ShouldCreateLeadership_WhenNoExistingFound()
    {
        var requestDto = BuildRequest("kurin");
        var command = new UpsertLeadershipCommand(requestDto);
        var entity = BuildLeadershipEntity();

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Leadership?)null);

        SetupCreateMapping(command, entity);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(entity.LeadershipKey, result.Data!.LeadershipKey);
        Assert.Equal("GetLeadershipByKey", result.CreatedAtActionName);
        Assert.NotNull(result.CreatedAtRouteValues);

        _leadershipRepoMock.Verify(r => r.Add(entity, It.IsAny<CancellationToken>()), Times.Once);
        _leadershipRepoMock.Verify(r => r.Update(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateLeadership_WhenProvidedKeyNotFound()
    {
        var providedKey = Guid.NewGuid();
        var requestDto = BuildRequest("group");
        var command = new UpsertLeadershipCommand(requestDto, providedKey);
        var entity = BuildLeadershipEntity(providedKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(providedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Leadership?)null);

        SetupCreateMapping(command, entity);
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
        var entity = BuildLeadershipEntity();

        SetupCreateMapping(command, entity);
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
        var entity = BuildLeadershipEntity();

        SetupCreateMapping(command, entity);
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
        var entity = BuildLeadershipEntity();

        SetupCreateMapping(command, entity);
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

        SetupUpdateMapping(command, existing);
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

        SetupUpdateMapping(command, existing);
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

        SetupUpdateMapping(command, existing);
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

        SetupUpdateMapping(command, existing);
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

        SetupUpdateMapping(command, existing);
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
        SetupUpdateMapping(command, existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(oldMemberKey)), Times.Once);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(newMemberKey)), Times.Once);
        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(stayingMemberKey)), Times.Never);
        _cacheMock.Verify(c => c.Invalidate(BackendCachePolicies.MentorScopeReads), Times.Once);
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
        SetupUpdateMapping(command, existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await _handler.Handle(command, CancellationToken.None);

        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditArea.Office, entry.Area);
        Assert.Equal(AuditActions.Office.Assigned, entry.Action);
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
        SetupUpdateMapping(command, existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        var entry = Assert.Single(_journal.Entries);
        Assert.Equal(AuditActions.Office.Ended, entry.Action);
        Assert.Equal("Писар", entry.SubjectLabel);
        Assert.Equal(memberKey, entry.SubjectMemberKey);
        Assert.Equal(new[] { new AuditChange("holder", "A B", null) }, entry.Changes);
    }

    [Fact]
    public async Task Handle_ShouldMarkNobody_WhenTheSameHoldersAreResubmitted()
    {
        var memberKey = Guid.NewGuid();
        var existing = LeadershipHeldBy((LeadershipRole.Kurinnuy, memberKey));
        var command = new UpsertLeadershipCommand(
            RequestSeating(existing, (LeadershipRole.Kurinnuy, memberKey)),
            existing.LeadershipKey);
        SetupUpdateMapping(command, existing);
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
        SetupUpdateMapping(command, existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(command, CancellationToken.None);

        _accessChangesMock.Verify(s => s.MarkUser(AccountOf(memberKey)), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCachePolicies.MentorScopeReads), Times.Once);
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
        var entity = BuildLeadershipEntity();

        SetupCreateMapping(command, entity);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.InternalServerError, result.Type);
        Assert.Null(result.Data);
        _mapperMock.Verify(m => m.Map<LeadershipResponse>(It.IsAny<Leadership>()), Times.Never);
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

        SetupUpdateMapping(command, existing);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.InternalServerError, result.Type);
        Assert.Null(result.Data);
        _mapperMock.Verify(m => m.Map<LeadershipResponse>(It.IsAny<Leadership>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenInvalidType()
    {
        var requestDto = BuildRequest("INVALID_TYPE");
        var command = new UpsertLeadershipCommand(requestDto);

        var entity = BuildLeadershipEntity();
        // Mapping for creation still needed before it attempts Enum.Parse
        _mapperMock
            .Setup(m => m.Map<Leadership>(command))
            .Returns(entity);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldTreatGuidEmptyLeadershipKey_AsCreate()
    {
        var requestDto = BuildRequest("group");
        // Mimic constructing with an empty leadership key (edge case)
        var command = new UpsertLeadershipCommand(requestDto, Guid.Empty);
        var entity = BuildLeadershipEntity();

        SetupCreateMapping(command, entity);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(ResultType.Created, result.Type);
        _leadershipRepoMock.Verify(r => r.Add(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Once);
        _leadershipRepoMock.Verify(r => r.Update(It.IsAny<Leadership>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
