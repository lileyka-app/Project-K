using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MentorAssignment;

/// <summary>
/// Maps for real on purpose: the handler answers through <c>IMemberDirectory</c>, which hands
/// back <c>MemberSummary</c> records, and a mocked mapper once hid that no map from the record to
/// the DTO existed — every GET /group/{key}/mentors was a 500 in the e2e run while
/// the unit tests stayed green.
/// </summary>
public class GetGroupMentorsQueryHandlerTests
{
    private readonly Mock<IMentorAssignmentRepository> _assignments = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly GetGroupMentorsQueryHandler _handler;

    public GetGroupMentorsQueryHandlerTests()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.MentorAssignments).Returns(_assignments.Object);
        _handler = new GetGroupMentorsQueryHandler(unitOfWork.Object, _members.Object, FakeBlobReadLinks.Instance);
    }

    [Fact]
    public async Task Handle_ShouldListEachActiveMentorOnce_MappedFromTheDirectoryRecord()
    {
        var groupKey = Guid.NewGuid();
        var mentorAccount = Guid.NewGuid();
        var revokedAccount = Guid.NewGuid();
        _assignments
            .Setup(r => r.GetByGroupKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Common.Entities.KurinModule.MentorAssignment { GroupKey = groupKey, MentorUserKey = mentorAccount },
                new Common.Entities.KurinModule.MentorAssignment { GroupKey = groupKey, MentorUserKey = mentorAccount },
                new Common.Entities.KurinModule.MentorAssignment { GroupKey = groupKey, MentorUserKey = revokedAccount, RevokedAtUtc = DateTime.UtcNow }
            ]);
        var mentor = new MemberSummary(Guid.NewGuid(), mentorAccount, Guid.NewGuid(), groupKey, "Оксана", "Виховниця", "o@example.com", null);
        _members.Setup(d => d.FindByAccountAsync(mentorAccount, It.IsAny<CancellationToken>())).ReturnsAsync(mentor);

        var result = await _handler.Handle(new GetGroupMentorsQuery(groupKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        var mentors = result.Data!.ToList();
        Assert.Single(mentors);
        Assert.Equal(mentor.MemberKey, mentors[0].MemberKey);
        Assert.Equal(mentorAccount, mentors[0].UserKey);
        Assert.Equal("Оксана", mentors[0].FirstName);
        Assert.Equal("Виховниця", mentors[0].LastName);
        _members.Verify(d => d.FindByAccountAsync(revokedAccount, It.IsAny<CancellationToken>()), Times.Never);
    }
}
