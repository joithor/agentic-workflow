using System.Globalization;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Tests.Workflow.Registry;

/// <summary>The approve preview's diff (engine spec §4.2): unified hunks with three lines of context.</summary>
public sealed class LineDiffTests
{
    [Fact, Trait("Lane", "unit")]
    public void Changed_line_is_one_hunk_with_its_context()
    {
        var before = string.Join('\n', Enumerable.Range(1, 9).Select(i => "line " + i.ToString(CultureInfo.InvariantCulture)));
        var after = before.Replace("line 5", "line five", StringComparison.Ordinal);

        Assert.Equal("--- a\n+++ b\n@@ -2,7 +2,7 @@\n line 2\n line 3\n line 4\n-line 5\n+line five\n line 6\n line 7\n line 8\n", LineDiff.Unified(before, after, "a", "b"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Equal_texts_have_only_the_headers() => Assert.Equal("--- a\n+++ b\n", LineDiff.Unified("x\ny", "x\ny", "a", "b"));
}
