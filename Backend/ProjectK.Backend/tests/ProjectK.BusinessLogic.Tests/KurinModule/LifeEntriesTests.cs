using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Life;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>What a person may write into their own life story, and how a вишкіл is named.</summary>
public class LifeEntriesTests
{
    private static SaveMyLifeEntryCommand Command(SaveLifeEntryRequest entry) => new(null, entry);

    private static SaveLifeEntryRequest Training(TrainingCourse? course, string? title = null) => new()
    {
        Kind = LifeEntryKind.Training,
        Course = course,
        Title = title,
        StartDate = new DateOnly(2024, 7, 1)
    };

    [Fact]
    public void A_known_course_needs_no_name()
    {
        new SaveMyLifeEntryCommandValidator().Validate(Command(Training(TrainingCourse.Kvz))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Another_course_and_every_camp_need_a_name()
    {
        var validator = new SaveMyLifeEntryCommandValidator();

        validator.Validate(Command(Training(TrainingCourse.Other))).IsValid.Should().BeFalse();
        validator.Validate(Command(Training(null))).IsValid.Should().BeFalse();
        validator.Validate(Command(new SaveLifeEntryRequest { Kind = LifeEntryKind.Camp, StartDate = new DateOnly(2024, 7, 1) }))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void It_cannot_end_before_it_starts()
    {
        var entry = Training(TrainingCourse.LisovaShkola);
        entry.EndDate = new DateOnly(2024, 6, 30);

        new SaveMyLifeEntryCommandValidator().Validate(Command(entry)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_camp_keeps_no_course_or_number_and_blanks_become_nothing()
    {
        var stored = new MemberLifeEntry();

        LifeMapping.Apply(stored, new SaveLifeEntryRequest
        {
            Kind = LifeEntryKind.Camp,
            Course = TrainingCourse.Kvz,
            Number = 4,
            Title = "  Кременецький табір ",
            StartDate = new DateOnly(2024, 7, 1),
            Commandant = "   "
        });

        stored.Course.Should().BeNull();
        stored.Number.Should().BeNull();
        stored.Title.Should().Be("Кременецький табір");
        stored.Commandant.Should().BeNull();
    }

    [Fact]
    public void A_known_course_drops_a_stray_name()
    {
        var stored = new MemberLifeEntry();

        LifeMapping.Apply(stored, Training(TrainingCourse.Kvz, "щось"));

        stored.Title.Should().BeNull();
    }

    [Theory]
    [InlineData(TrainingCourse.Kvz, null, 12, "12-й КВЗ")]
    [InlineData(TrainingCourse.LisovaShkola, null, null, "ЛШ")]
    [InlineData(TrainingCourse.Other, "Вишкіл писарів", 3, "3-й Вишкіл писарів")]
    public void A_course_reads_by_its_number_and_name(TrainingCourse course, string? title, int? number, string expected)
    {
        ExportMyLifePdfQueryHandler.TrainingName(course, title, number).Should().Be(expected);
    }
}
