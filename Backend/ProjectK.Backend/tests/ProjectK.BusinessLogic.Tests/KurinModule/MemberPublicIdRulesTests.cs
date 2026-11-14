using FluentAssertions;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>
/// The code is drawn, not derived: whoever sees a member's key — every leader, in every card's
/// address — must not be able to work it out.
/// </summary>
public class MemberPublicIdRulesTests
{
    [Fact]
    public void New_ShouldUseNoAmbiguousCharacters()
    {
        // Hexadecimal only: nothing here can be misheard as another character when read aloud.
        MemberPublicIdRules.New().Should().MatchRegex("^PL-[0-9A-F]{5}-[0-9A-F]{5}$");
    }

    [Fact]
    public void New_ShouldNotRepeat()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => MemberPublicIdRules.New()).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }
}
