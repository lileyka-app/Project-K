using FluentAssertions;
using ProjectK.Common.Phones;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("+380501234567", "+380501234567")]
    [InlineData("+380 50 123 45 67", "+380501234567")]
    [InlineData(" +420 (777) 123-456 ", "+420777123456")]
    [InlineData("+1.202.555.0100", "+12025550100")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_DropsSeparators(string? raw, string expected)
    {
        PhoneNumbers.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("0501234567")]
    [InlineData("380501234567")]
    [InlineData("+0501234567")]
    [InlineData("+380 50 abc")]
    [InlineData("+12345")]
    [InlineData("+38050123456789012345")]
    public void Normalize_RejectsWhatIsNotAnInternationalNumber(string raw)
    {
        PhoneNumbers.Normalize(raw).Should().BeNull();
    }

    [Fact]
    public void Normalize_KeepsTwentyCharactersAtMost()
    {
        PhoneNumbers.Normalize("+3805012345678901234"[..20]).Should().HaveLength(20);
        PhoneNumbers.Normalize("+380501234567890123456").Should().BeNull();
    }

    [Theory]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("(050) 123-45-67", "+380501234567")]
    [InlineData("380501234567", "+380501234567")]
    [InlineData("00420777123456", "+420777123456")]
    [InlineData("+420777123456", "+420777123456")]
    public void NormalizeLegacy_ReadsOldUkrainianSpellings(string raw, string expected)
    {
        PhoneNumbers.NormalizeLegacy(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("501234567")]
    [InlineData("тел. 050")]
    public void NormalizeLegacy_LeavesAmbiguousNumbersUnread(string raw)
    {
        PhoneNumbers.NormalizeLegacy(raw).Should().BeNull();
    }
}
