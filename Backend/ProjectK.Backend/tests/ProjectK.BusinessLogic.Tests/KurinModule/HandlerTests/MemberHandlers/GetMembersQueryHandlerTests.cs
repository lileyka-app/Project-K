using ProjectK.BusinessLogic.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Members;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.Services.BlobStorageService;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

public class GetMembersHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _uowMock;
    private readonly Mock<IMemberRepository> _memberRepoMock;
    private readonly Mock<IMentorAssignmentRepository> _mentorRepoMock;
    private readonly IMapper _mapper;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly GetMembersQueryHandler _handler;

    public GetMembersHandlerTests()
    {
        _memberRepoMock = new Mock<IMemberRepository>();
        _mentorRepoMock = new Mock<IMentorAssignmentRepository>();
        _uowMock = new Mock<IMemberUnitOfWork>();
        _uowMock.Setup(u => u.Members).Returns(_memberRepoMock.Object);

        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _currentUserContextMock.Setup(c => c.IsInRole(It.IsAny<string>())).Returns(true); // Allow all by default for tests

        var loggerFactory = LoggerFactory.Create(builder => { });

        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, loggerFactory);
        _mapper = mapperConfig.CreateMapper();

        _handler = new GetMembersQueryHandler(_uowMock.Object, _mapper, FieldAccess.SeesEverything());
    }

    private static MemberListItemDto MakeItem(Guid groupKey, Guid kurinKey, string first, string last,
        string? blob = null, Guid? userKey = null, string? role = null) =>
        new()
        {
            MemberKey = Guid.NewGuid(),
            GroupKey = groupKey,
            KurinKey = kurinKey,
            UserKey = userKey,
            UserRole = role,
            FirstName = first,
            MiddleName = "M",
            LastName = last,
            Email = $"{first.ToLower()}@example.com",
            PhoneNumber = "123456",
            DateOfBirth = new DateOnly(2000, 1, 1),
            ProfilePhotoBlobName = blob
        };

    [Fact]
    public async Task Handle_GroupKeyOnly_ShouldReturnMembersFromGroup()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListItemDto>
        {
            MakeItem(groupKey, kurinKey, "A","One","a.png"),
            MakeItem(groupKey, kurinKey, "B","Two", null)
        };

        _memberRepoMock
            .Setup(r => r.GetListItemsByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var query = new GetMembersQuery(groupKey, Guid.Empty);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(2);
        var list = result.Data!.ToList();
        list[0].FirstName.Should().Be("A");
        list[0].ProfilePhotoUrl.Should().Be("https://cdn.test/a.png?sig=read");
        list[1].FirstName.Should().Be("B");
        list[1].ProfilePhotoUrl.Should().BeNull();

        _memberRepoMock.Verify(r => r.GetListItemsByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
        _memberRepoMock.Verify(r => r.GetListItemsByKurinKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KurinKeyOnly_ShouldReturnMembersFromKurin()
    {
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListItemDto>
        {
            MakeItem(Guid.NewGuid(), kurinKey, "C", "Three"),
            MakeItem(Guid.NewGuid(), kurinKey, "D", "Four")
        };

        _memberRepoMock
            .Setup(r => r.GetListItemsByKurinKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var query = new GetMembersQuery(Guid.Empty, kurinKey);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(2);
        result.Data!.Select(x => x.FirstName).Should().BeEquivalentTo(new[] { "C", "D" });

        _memberRepoMock.Verify(r => r.GetListItemsByKurinKeyAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
        _memberRepoMock.Verify(r => r.GetListItemsByGroupKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldIncludeUserRole_WhenMemberHasLinkedUser()
    {
        var kurinKey = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        var member = MakeItem(Guid.NewGuid(), kurinKey, "Lead", "Mentor", userKey: userKey, role: "Group.Hurtkoviy");

        _memberRepoMock
            .Setup(r => r.GetListItemsByKurinKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberListItemDto> { member });

        var result = await _handler.Handle(new GetMembersQuery(Guid.Empty, kurinKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.Single().UserRole.Should().Be("Group.Hurtkoviy");
    }

    [Fact]
    public async Task Handle_BothKeysProvided_ShouldReturnBadRequest()
    {
        var query = new GetMembersQuery(Guid.NewGuid(), Guid.NewGuid());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.Data.Should().BeNull();

        _memberRepoMock.Verify(r => r.GetListItemsByGroupKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _memberRepoMock.Verify(r => r.GetListItemsByKurinKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GroupKeyOnly_NoMembers_ShouldReturnEmptySuccess()
    {
        var groupKey = Guid.NewGuid();
        _memberRepoMock
            .Setup(r => r.GetListItemsByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemberListItemDto>());

        var result = await _handler.Handle(new GetMembersQuery(groupKey, Guid.Empty), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        var groupKey = Guid.NewGuid();
        var expected = new Exception("DB error");
        _memberRepoMock
            .Setup(r => r.GetListItemsByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var query = new GetMembersQuery(groupKey, Guid.Empty);

        var ex = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, CancellationToken.None));
        ex.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_MappingConsistency_ShouldMatchDirectMapping()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberListItemDto>
        {
            MakeItem(groupKey, kurinKey, "X","One","pic1.jpg"),
            MakeItem(groupKey, kurinKey, "Y","Two","pic 2.png")
        };

        _memberRepoMock
            .Setup(r => r.GetListItemsByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var result = await _handler.Handle(new GetMembersQuery(groupKey, Guid.Empty), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        var direct = _mapper.Map<IEnumerable<MemberResponse>>(members);
        result.Data.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task Handle_ShouldMaskEachRow_ByTheGroupItStandsIn()
    {
        var viewer = Guid.NewGuid();
        var ownGroup = Guid.NewGuid();
        var otherGroup = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var mate = MakeItem(ownGroup, kurinKey, "Mate", "Own");
        var stranger = MakeItem(otherGroup, kurinKey, "Stranger", "Other");
        var self = MakeItem(otherGroup, kurinKey, "Self", "Me", userKey: viewer);
        foreach (var row in new[] { mate, stranger, self })
        {
            row.Address = "Street 1";
            row.DateOfBirth = new DateOnly(2011, 3, 9);
        }

        _memberRepoMock
            .Setup(r => r.GetListItemsByKurinKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mate, stranger, self]);
        var mask = new MemberFieldMask(
            viewer,
            new Dictionary<MemberDataGroup, CapabilityReach>
            {
                [MemberDataGroup.Contacts] = CapabilityReach.OwnGroup,
                [MemberDataGroup.Birthday] = CapabilityReach.Kurin
            },
            [ownGroup]);
        var handler = new GetMembersQueryHandler(_uowMock.Object, _mapper, FieldAccess.Of(mask));

        var result = (await handler.Handle(new GetMembersQuery(Guid.Empty, kurinKey), CancellationToken.None)).Data!.ToList();

        var mateRow = result.Single(m => m.FirstName == "Mate");
        mateRow.Email.Should().Be("mate@example.com");
        mateRow.DateOfBirth.Should().BeNull();
        mateRow.Birthday.Should().Be("03-09");
        mateRow.Address.Should().BeNull();
        mateRow.HiddenData.Should().Equal("birthDate", "address", "parents", "extras");

        var strangerRow = result.Single(m => m.FirstName == "Stranger");
        strangerRow.Email.Should().BeEmpty();
        strangerRow.PhoneNumber.Should().BeEmpty();
        strangerRow.HiddenData.Should().Equal("contacts", "birthDate", "address", "parents", "extras");

        var selfRow = result.Single(m => m.FirstName == "Self");
        selfRow.Email.Should().Be("self@example.com");
        selfRow.DateOfBirth.Should().Be(new DateOnly(2011, 3, 9));
        selfRow.Address.Should().Be("Street 1");
        selfRow.HiddenData.Should().BeEmpty();
    }
}
