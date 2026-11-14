using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Review;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberAwardHandlers;

/// <summary>
/// Value types have defaults, and a command built out of them cannot tell "нуль" from "не сказано".
/// Every case here was reachable over HTTP and answered 200 while storing, or deciding, something
/// nobody asked for.
/// </summary>
public sealed class SilentDefaultsAreRefusedTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private static UpsertMemberAwardCommandValidator AwardValidator()
        => new(new FixedTimeProvider(Now));

    private static UpsertMemberAwardCommand Award(DateTime? date = null, MemberAwardLevel? level = null) => new()
    {
        MemberKey = Guid.NewGuid(),
        Level = level ?? MemberAwardLevel.First,
        DateAcquired = date ?? Now.AddDays(-1),
        Note = null
    };

    [Fact]
    public void Award_ShouldBeAcceptedWhenItSaysWhenAndWhich()
    {
        AwardValidator().Validate(Award()).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// The one that was found: a body without <c>dateAcquired</c> stored
    /// <see cref="DateTime.MinValue"/> and the card then read «0001». Found by sending the field
    /// under the wrong name, which is how a client would meet it.
    /// </summary>
    [Fact]
    public void Award_ShouldRefuseAMissingDate()
    {
        var result = AwardValidator().Validate(Award(date: default(DateTime)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertMemberAwardCommand.DateAcquired));
    }

    [Fact]
    public void Award_ShouldRefuseADateInTheFuture()
    {
        AwardValidator().Validate(Award(date: Now.AddDays(30))).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A провід in Kyiv recording today's відзначення in the evening is already tomorrow in UTC.
    /// Refusing that would be a rule the person cannot see and cannot satisfy.
    /// </summary>
    [Fact]
    public void Award_ShouldAllowTodayFromAZoneAheadOfUtc()
    {
        AwardValidator().Validate(Award(date: Now.AddHours(6))).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// <c>MemberAwardLevel</c> starts at 1 so that the unset default is not a valid level. Nothing
    /// enforced that intent at the boundary, so a body with no level stored 0.
    /// </summary>
    [Fact]
    public void Award_ShouldRefuseAnUnsetLevel()
    {
        AwardValidator().Validate(Award(level: default(MemberAwardLevel))).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// The verdict used to be a plain <c>bool</c>: a body that never mentioned it read as
    /// <c>false</c> and **refused** the thing under review, with a 200 and no sign anything was
    /// wrong. Silence is now its own answer, and it is not a valid one.
    /// </summary>
    [Fact]
    public void BadgeReview_ShouldRefuseAnUnsaidVerdict()
    {
        var validator = new ReviewBadgeProgressCommandValidator();

        validator.Validate(new ReviewBadgeProgressCommand(Guid.NewGuid(), "badge-1", null, null))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BadgeReview_ShouldAcceptEitherVerdict(bool approved)
    {
        var validator = new ReviewBadgeProgressCommandValidator();

        validator.Validate(new ReviewBadgeProgressCommand(Guid.NewGuid(), "badge-1", approved, null))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void AwardReview_ShouldRefuseAnUnsaidVerdict()
    {
        var validator = new ReviewMemberAwardCommandValidator();

        validator.Validate(new ReviewMemberAwardCommand { MemberAwardKey = Guid.NewGuid(), IsApproved = null })
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AwardReview_ShouldAcceptEitherVerdict(bool approved)
    {
        var validator = new ReviewMemberAwardCommandValidator();

        validator.Validate(new ReviewMemberAwardCommand { MemberAwardKey = Guid.NewGuid(), IsApproved = approved })
            .IsValid.Should().BeTrue();
    }
}
