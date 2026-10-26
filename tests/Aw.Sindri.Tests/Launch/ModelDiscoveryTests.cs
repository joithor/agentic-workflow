using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch;

public sealed class ModelDiscoveryTests
{
    private static string CodexFixture => File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "codex-models.json"));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_keeps_the_listed_models_in_printed_order()
    {
        var models = CodexModels.Read(CodexFixture);

        Assert.Equal(["gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.3-codex", "gpt-5.2"], models.Select(m => m.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_drops_hidden_models() =>
        Assert.DoesNotContain(CodexModels.Read(CodexFixture), m => string.Equals(m.Id, "codex-auto-review", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_carries_display_name_priority_and_source()
    {
        var mini = CodexModels.Read(CodexFixture).Single(m => string.Equals(m.Id, "gpt-5.4-mini", StringComparison.Ordinal));

        Assert.Equal(new DiscoveredModel("gpt-5.4-mini", "GPT-5.4-Mini", 4, "codex debug models"), mini);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"models\":null}")]
    public void Codex_reader_returns_nothing_for_text_that_is_not_the_catalog(string text) =>
        Assert.Empty(CodexModels.Read(text));

    [Fact, Trait("Lane", "unit")]
    public void Codex_reader_skips_a_row_without_a_slug_and_treats_missing_visibility_as_hidden() =>
        Assert.Equal(["a"], CodexModels.Read("{\"models\":[{\"visibility\":\"list\"},{\"slug\":\"a\",\"visibility\":\"list\"},{\"slug\":\"b\"}]}").Select(m => m.Id), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Claude_catalog_lists_the_aliases_cheapest_first() =>
        Assert.Equal(["haiku", "sonnet", "opus"], ClaudeModels.Catalog.Select(m => m.Id), StringComparer.Ordinal);
}
