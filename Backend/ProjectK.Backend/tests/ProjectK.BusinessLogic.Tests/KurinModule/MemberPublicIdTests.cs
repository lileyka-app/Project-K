using FluentAssertions;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>
/// The code is drawn, not derived: whoever sees a member's key — every leader, in every card's
/// address — must not be able to work it out.
/// </summary>
public class MemberPublicIdTests
{
    [Fact]
    public void New_ShouldUseNoAmbiguousCharacters()
    {
        // Hexadecimal only: nothing here can be misheard as another character when read aloud.
        MemberPublicId.New().Should().MatchRegex("^PL-[0-9A-F]{5}-[0-9A-F]{5}$");
    }

    [Fact]
    public void New_ShouldNotRepeat()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => MemberPublicId.New()).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }
}
