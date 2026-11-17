using FluentAssertions;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>
/// The three name shapes keep the exact output of the code they replaced: <c>FirstLast</c> trims
/// the ends only, the other two drop blank parts and leave the rest untouched.
/// </summary>
public class PersonNamesTests
{
    [Theory]
    [InlineData("Оксана", "Петренко", "Оксана Петренко")]
    [InlineData("Оксана", "", "Оксана")]
    [InlineData("", "Петренко", "Петренко")]
    [InlineData(null, null, "")]
    [InlineData("  ", "  ", "")]
    [InlineData(" Оксана ", " Петренко ", "Оксана   Петренко")]
    public void FirstLast_TrimsOnlyTheEnds(string? first, string? last, string expected)
    {
        PersonNames.FirstLast(first, last).Should().Be(expected);
    }

    [Theory]
    [InlineData("Оксана", "Іванівна", "Петренко", "Оксана Іванівна Петренко")]
    [InlineData("Оксана", null, "Петренко", "Оксана Петренко")]
    [InlineData("Оксана", "   ", "Петренко", "Оксана Петренко")]
    [InlineData(null, null, null, "")]
    [InlineData(" Оксана", null, "Петренко ", " Оксана Петренко ")]
    public void Full_DropsBlankPartsAndKeepsTheRestAsStored(string? first, string? middle, string? last, string expected)
    {
        PersonNames.Full(first, middle, last).Should().Be(expected);
    }

    [Theory]
    [InlineData("Петренко", "Оксана", "Іванівна", "Петренко Оксана Іванівна")]
    [InlineData("Петренко", "Оксана", null, "Петренко Оксана")]
    [InlineData("", "Оксана", "Іванівна", "Оксана Іванівна")]
    [InlineData(null, null, null, "")]
    public void SurnameFirst_PutsTheSurnameFirstAndDropsBlankParts(string? last, string? first, string? middle, string expected)
    {
        PersonNames.SurnameFirst(last, first, middle).Should().Be(expected);
    }

    [Fact]
    public void SurnameFirst_LeavesThePoBatkoviOutByDefault()
    {
        PersonNames.SurnameFirst("Петренко", "Оксана").Should().Be("Петренко Оксана");
    }
}
