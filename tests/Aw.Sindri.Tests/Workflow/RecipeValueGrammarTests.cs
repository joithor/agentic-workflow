using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow;

public sealed class RecipeValueGrammarTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("1m", 1)]
    [InlineData("10m", 10)]
    [InlineData("4h", 240)]
    [InlineData("2d", 2880)]
    public void A_duration_is_a_number_and_m_h_or_d(string text, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), RecipeDuration.From(text).Span);

    [Theory, Trait("Lane", "unit")]
    [InlineData("0m")]
    [InlineData("30s")]
    [InlineData("05m")]
    [InlineData("1w")]
    [InlineData("h")]
    [InlineData("1234567d")]
    public void A_duration_below_a_minute_or_out_of_grammar_is_refused(string text) =>
        Assert.False(RecipeDuration.TryFrom(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData("1500", 1500)]
    [InlineData("200k", 200_000)]
    [InlineData("2M", 2_000_000)]
    public void Tokens_are_a_number_or_k_or_M(string text, int tokens)
    {
        Assert.True(TokenCounts.TryParse(text, out var count));
        Assert.Equal(tokens, count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2K")]
    [InlineData("1.5k")]
    [InlineData("3000M")]
    [InlineData("")]
    public void Tokens_out_of_grammar_are_refused(string text) =>
        Assert.False(TokenCounts.TryParse(text, out _));

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, "1")]
    [InlineData(12_500, "12500")]
    [InlineData(100_000, "100k")]
    [InlineData(1_500_000, "1500k")]
    [InlineData(2_000_000, "2M")]
    public void Format_writes_whole_k_or_M_else_the_plain_integer(int count, string text) => Assert.Equal(text, TokenCounts.Format(count));

    [Fact, Trait("Lane", "unit")]
    public void Format_round_trips_through_TryParse_for_odd_and_even_budgets()
    {
        var counts = Enumerable.Range(1, 3_000).Concat([12_499, 12_500, 12_501, 24_999, 25_000, 999_999, 1_000_000, 1_000_001, 1_500_000, 1_999_999, 2_147_000_000, int.MaxValue]);
        foreach (var count in counts)
        {
            Assert.True(TokenCounts.TryParse(TokenCounts.Format(count), out var back), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} -> {TokenCounts.Format(count)}"));
            Assert.Equal(count, back);
        }
    }
}
