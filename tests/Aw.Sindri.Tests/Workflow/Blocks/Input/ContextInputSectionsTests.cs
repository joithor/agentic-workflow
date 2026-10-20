using Aw.Kernel.Testing;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

[Lane(TestLane.Unit)]
public sealed class ContextInputSectionsTests
{
    // A code Step reads the typed result, not the text: a lenses view of a large map is over the text's 120 KiB as JSON, and dropping it parked the
    // gather. The typed result is kept up to the runner's own cap for it (256 KiB).
    [Fact]
    public void A_typed_result_over_the_text_budget_but_under_the_runners_cap_is_kept()
    {
        var view = new List<string> { new('x', 200 * 1024) };

        var filled = ContextInputSections.Filled("context.lenses", "a short text\n", [], view, new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });

        _ = Assert.NotNull(filled.Result);
        Assert.DoesNotContain("left out", filled.Sections["context.lenses"], StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_counts_only_real_lines_left_out_not_the_empty_piece_after_the_last_newline()
    {
        var line = new string('x', 99) + "\n";   // 100 bytes
        var text = string.Concat(Enumerable.Repeat(line, (ContextInputSections.Budget / 100) + 50));   // over the budget, ends with a newline

        var bound = ContextInputSections.Bound(text);

        var shown = bound.Split('\n').Count(static l => l.Length == 99);
        var total = (ContextInputSections.Budget / 100) + 50;
        Assert.Contains(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"[{total - shown} more lines not shown"), bound, StringComparison.Ordinal);
    }
}
