using Aw.Kernel.Testing;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

[Lane(TestLane.Unit)]
public sealed class ContextInputSectionsTests
{
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
