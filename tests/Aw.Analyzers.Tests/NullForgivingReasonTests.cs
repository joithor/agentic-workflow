using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class NullForgivingReasonTests
{
    private const string Sample = """
        namespace Aw.Probe;
        internal sealed class C
        {
            private string _a = null!;
            private string _b = null!; // why: set in Init before any read
            // why: the dictionary always holds the key after Seed
            private string _c = null!;
            public string M(string? s, object? o)
            {
                var x = s!;
                var y = s!; // why: checked by the caller
                // why: o is set on every path that reaches here
                var z = o!.ToString();
                var w = s!; //why:
                return x + y + z + w + _a + _b + _c;
            }
        }
        """;

    [Fact]
    public async Task Flags_every_bang_without_a_why_comment()
    {
        var found = await AnalyzerHarness.Run(new NullForgivingReasonAnalyzer(), "Aw.Probe", Sample);

        // Line 13: '//why:' with no reason doesn't count.
        Assert.Equal(["AW1002@13", "AW1002@4", "AW1002@9"], found);
    }

    [Fact]
    public async Task Why_in_a_string_literal_is_not_a_comment()
    {
        var found = await AnalyzerHarness.Run(
            new NullForgivingReasonAnalyzer(), "Aw.Probe", "namespace Aw.Probe;\ninternal static class C { internal static string M(string? s) => s! + \"// why: no\"; }\n");

        Assert.Equal(["AW1002@2"], found);
    }
}
