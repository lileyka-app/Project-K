using FluentAssertions;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class PhoneNumberRulesTests
{
    [Theory]
    [InlineData("+380501234567", "+380501234567")]
    [InlineData("+380 50 123 45 67", "+380501234567")]
    [InlineData(" +420 (777) 123-456 ", "+420777123456")]
    [InlineData("+1.202.555.0100", "+12025550100")]
    [InlineData("+123 4567 8901 2345", "+123456789012345")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_DropsSeparators(string? raw, string expected)
    {
        PhoneNumberRules.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("0501234567")]
    [InlineData("380501234567")]
    [InlineData("+0501234567")]
    [InlineData("+380 50 abc")]
    [InlineData("+12345")]
    [InlineData("+38050123456789012345")]
    [InlineData("+1234567890123456")]
    public void Normalize_RejectsWhatIsNotAnInternationalNumber(string raw)
    {
        PhoneNumberRules.Normalize(raw).Should().BeNull();
    }

    [Fact]
    public void Normalize_TakesFifteenDigitsAtMostForAnyCountry()
    {
        PhoneNumberRules.Normalize("+380501234567890").Should().HaveLength(16);
        PhoneNumberRules.Normalize("+3805012345678901").Should().BeNull();
        PhoneNumberRules.MaxLength.Should().BeGreaterThanOrEqualTo(16);
    }

    [Theory]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("(050) 123-45-67", "+380501234567")]
    [InlineData("380501234567", "+380501234567")]
    [InlineData("00420777123456", "+420777123456")]
    [InlineData("+420777123456", "+420777123456")]
    public void NormalizeLegacy_ReadsOldUkrainianSpellings(string raw, string expected)
    {
        PhoneNumberRules.NormalizeLegacy(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("501234567")]
    [InlineData("тел. 050")]
    public void NormalizeLegacy_LeavesAmbiguousNumbersUnread(string raw)
    {
        PhoneNumberRules.NormalizeLegacy(raw).Should().BeNull();
    }
}
