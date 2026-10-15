using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The token counts status prints (review run 1, code-reviewer-10).</summary>
public sealed class StatusTextTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(0, "0")]
    [InlineData(950, "950")]
    [InlineData(1_500, "1.5k")]
    [InlineData(200_000, "200k")]
    [InlineData(999_000, "999k")]
    [InlineData(1_000_000, "1M")]
    [InlineData(1_500_000, "1.5M")]
    [InlineData(2_000_000, "2M")]
    public void Tokens_print_in_the_largest_unit_that_keeps_them_short(int tokens, string text) =>
        Assert.Equal(text, StatusText.Tokens(tokens));
}
