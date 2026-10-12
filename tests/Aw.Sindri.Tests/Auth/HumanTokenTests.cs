using Aw.Sindri.Core.Auth;

namespace Aw.Sindri.Tests.Auth;

public sealed class HumanTokenTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("abc", true)]
    [InlineData("abd", false)]
    [InlineData("ab", false)]
    [InlineData(null, false)]
    public void A_token_matches_only_its_exact_value(string? presented, bool matches) =>
        Assert.Equal(matches, new HumanToken("abc").Matches(presented));

    [Fact, Trait("Lane", "unit")]
    public void A_token_never_prints_its_value() =>
        Assert.DoesNotContain("secret", new HumanToken("secret").ToString(), StringComparison.Ordinal);
}
