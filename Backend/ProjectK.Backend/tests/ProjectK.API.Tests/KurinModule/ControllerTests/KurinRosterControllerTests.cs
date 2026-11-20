using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.API.Tests.KurinModule.ControllerTests;

public class KurinRosterControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly KurinRosterController _controller;

    public KurinRosterControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new KurinRosterController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetAllByGroup_ShouldReturnOk_WhenSuccess()
    {
        var groupKey = Guid.NewGuid();
        var members = new List<MemberDto>
        {
            TestMemberDto.Blank with { MemberKey = Guid.NewGuid(), GroupKey = Guid.NewGuid(), KurinKey = Guid.NewGuid(), FirstName = "A", LastName = "L", MiddleName="M", Email="a@ex.com", PhoneNumber="1", DateOfBirth = new DateOnly(1990,1,1) },
            TestMemberDto.Blank with { MemberKey = Guid.NewGuid(), GroupKey = Guid.NewGuid(), KurinKey = Guid.NewGuid(), FirstName = "B", LastName = "L", MiddleName="M", Email="b@ex.com", PhoneNumber="2", DateOfBirth = new DateOnly(1991,1,1) }
        };
        var serviceResult = new ServiceResult<IEnumerable<MemberDto>>(ResultType.Success, members);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetAllByGroup(groupKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<MemberDto>>(ok.Value);
        Assert.Equal(members.Count, data.Count);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMembersQuery>(q => q.GroupKey == groupKey && q.KurinKey == Guid.Empty),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllByKurin_ShouldReturnOk_WhenSuccess()
    {
        var kurinKey = Guid.NewGuid();
        var members = new List<MemberDto>
        {
            TestMemberDto.Blank with { MemberKey = Guid.NewGuid(), GroupKey = Guid.NewGuid(), KurinKey = kurinKey, FirstName = "A", LastName = "L", MiddleName="M", Email="a@ex.com", PhoneNumber="1", DateOfBirth = new DateOnly(1990,1,1) },
            TestMemberDto.Blank with { MemberKey = Guid.NewGuid(), GroupKey = Guid.NewGuid(), KurinKey = kurinKey, FirstName = "B", LastName = "L", MiddleName="M", Email="b@ex.com", PhoneNumber="2", DateOfBirth = new DateOnly(1991,1,1) }
        };

        var serviceResult = new ServiceResult<IEnumerable<MemberDto>>(ResultType.Success, members);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetAllByKurin(kurinKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<MemberDto>>(ok.Value);
        Assert.Equal(members.Count, data.Count);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMembersQuery>(q => q.GroupKey == Guid.Empty && q.KurinKey == kurinKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
