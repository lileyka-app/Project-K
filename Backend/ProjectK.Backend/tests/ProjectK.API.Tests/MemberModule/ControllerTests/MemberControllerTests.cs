using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.MemberModule;
using ProjectK.API.Models.Requests;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.API.Tests.MemberModule.ControllerTests;

public class MemberControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly MemberController _controller;

    public MemberControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new MemberController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var dto = TestMemberDto.Blank with
        {
            MemberKey = key,
            GroupKey = Guid.NewGuid(),
            KurinKey = Guid.NewGuid(),
            FirstName = "Ivan",
            MiddleName = "I.",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "123",
            DateOfBirth = new DateOnly(2000, 1, 1)
        };
        var serviceResult = new ServiceResult<MemberDto>(ResultType.Success, dto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMemberByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<MemberDto>(ok.Value);
        Assert.Equal(key, data.MemberKey);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnNotFound_WhenNotFound()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<MemberDto>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetMemberByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        Assert.IsType<NotFoundObjectResult>(result);
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

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var request = new UpsertMemberRequest
        {
            GroupKey = Guid.NewGuid(),
            FirstName = "Ivan",
            MiddleName = "I.",
            LastName = "Petrenko",
            Email = "ivan@example.com",
            PhoneNumber = "123456",
            DateOfBirth = new DateOnly(2000, 5, 10)
        };

        var serviceResult = new ServiceResult<MemberDto>(
            ResultType.Created,
            TestMemberDto.Blank with
            {
                MemberKey = key,
                GroupKey = request.GroupKey!.Value,
                KurinKey = Guid.NewGuid(),
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                DateOfBirth = request.DateOfBirth
            },
            "GetByKey",
            new { memberKey = key });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var data = Assert.IsType<MemberDto>(created.Value);
        Assert.Equal(key, data.MemberKey);

        _mediatorMock.Verify(m => m.Send(
            It.Is<UpsertMemberCommand>(c =>
                c.GroupKey == request.GroupKey &&
                c.FirstName == request.FirstName &&
                c.LastName == request.LastName),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenInvalid()
    {
        var request = new UpsertMemberRequest
        {
            GroupKey = Guid.NewGuid(),
            FirstName = "Bad",
            MiddleName = "X",
            LastName = "User",
            Email = "bad@example.com",
            PhoneNumber = "000",
            DateOfBirth = new DateOnly(1999, 1, 1)
        };

        var serviceResult = new ServiceResult<MemberDto>(ResultType.BadRequest, TestMemberDto.Blank);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Create(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenSuccess()
    {
        var memberKey = Guid.NewGuid();
        var request = new UpsertMemberRequest
        {
            GroupKey = Guid.NewGuid(),
            FirstName = "Updated",
            MiddleName = "U.",
            LastName = "Name",
            Email = "upd@example.com",
            PhoneNumber = "555",
            DateOfBirth = new DateOnly(1995, 2, 2),
            Address = "Praha 3",
            School = "",
            MotherPhone = "+420777000222",
            RemoveProfilePhoto = false
        };

        var serviceResult = new ServiceResult<MemberDto>(ResultType.Success,
            TestMemberDto.Blank with
            {
                MemberKey = memberKey,
                GroupKey = request.GroupKey!.Value,
                KurinKey = Guid.NewGuid(),
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                DateOfBirth = request.DateOfBirth
            });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Update(memberKey, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<MemberDto>(ok.Value);
        Assert.Equal(memberKey, data.MemberKey);

        _mediatorMock.Verify(m => m.Send(
            It.Is<UpsertMemberCommand>(c => c.MemberKey == memberKey && c.FirstName == request.FirstName
                && c.Address == "Praha 3" && c.School == "" && c.MotherPhone == "+420777000222"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenNotFound()
    {
        var memberKey = Guid.NewGuid();
        var request = new UpsertMemberRequest
        {
            GroupKey = Guid.NewGuid(),
            FirstName = "X",
            MiddleName = "Y",
            LastName = "Z",
            Email = "x@example.com",
            PhoneNumber = "1",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };

        var serviceResult = new ServiceResult<MemberDto>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Update(memberKey, request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenInvalid()
    {
        var memberKey = Guid.NewGuid();
        var request = new UpsertMemberRequest
        {
            GroupKey = Guid.NewGuid(),
            FirstName = "Bad",
            MiddleName = "B",
            LastName = "User",
            Email = "b@example.com",
            PhoneNumber = "0",
            DateOfBirth = new DateOnly(1999, 1, 1)
        };

        var serviceResult = new ServiceResult<MemberDto>(ResultType.BadRequest, TestMemberDto.Blank);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Update(memberKey, request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task VerifyProfile_ShouldSendCommandWithNote_WhenSuccess()
    {
        var memberKey = Guid.NewGuid();
        var request = new VerifyMemberProfileRequest { Note = "Checked by phone." };
        var serviceResult = new ServiceResult<MemberDto>(
            ResultType.Success,
            TestMemberDto.Blank with
            {
                MemberKey = memberKey,
                ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedCurrent,
                ProfileVerificationNote = request.Note
            });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<VerifyMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.VerifyProfile(memberKey, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<MemberDto>(ok.Value);
        Assert.Equal(MemberProfileVerificationStatus.VerifiedCurrent, data.ProfileVerificationStatus);

        _mediatorMock.Verify(m => m.Send(
            It.Is<VerifyMemberProfileCommand>(c => c.MemberKey == memberKey && c.Note == request.Note),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyProfile_ShouldSendCommandWithNullNote_WhenRequestIsNull()
    {
        var memberKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<MemberDto>(
            ResultType.Success,
            TestMemberDto.Blank with { MemberKey = memberKey });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<VerifyMemberProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        await _controller.VerifyProfile(memberKey, null, CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<VerifyMemberProfileCommand>(c => c.MemberKey == memberKey && c.Note == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetProfileVerification_ShouldSendCommand_WhenSuccess()
    {
        var memberKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<MemberDto>(
            ResultType.Success,
            TestMemberDto.Blank with
            {
                MemberKey = memberKey,
                ProfileVerificationStatus = MemberProfileVerificationStatus.Unverified
            });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ResetMemberProfileVerificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.ResetProfileVerification(memberKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<MemberDto>(ok.Value);
        Assert.Equal(MemberProfileVerificationStatus.Unverified, data.ProfileVerificationStatus);

        _mediatorMock.Verify(m => m.Send(
            It.Is<ResetMemberProfileVerificationCommand>(c => c.MemberKey == memberKey),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenSuccess()
    {
        var memberKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<Unit>(ResultType.Success, Unit.Value);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(memberKey);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ShouldReturnInternalServerError_WhenUnexpected()
    {
        var memberKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<Unit>((ResultType)999);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(memberKey);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }

    [Fact]
    public async Task GetKurinMentorCandidates_ShouldReturnOk_WhenSuccess()
    {
        var kurinKey = Guid.NewGuid();
        var mentors = new List<MemberLookupDto>
        {
            new() { MemberKey = Guid.NewGuid(), UserKey = Guid.NewGuid(), FirstName = "Mentor", MiddleName = "M", LastName = "One" }
        };
        var serviceResult = new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, mentors);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinMentorCandidatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetKurinMentorCandidates(kurinKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<MemberLookupDto>>(ok.Value);
        Assert.Single(data);
        Assert.Equal("One", data[0].LastName);
    }
}
