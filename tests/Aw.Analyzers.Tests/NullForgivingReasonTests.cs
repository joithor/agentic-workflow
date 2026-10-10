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

        // Line 14: '//why:' (no space) isn't the marker.
        Assert.Equal(["AW1002@10", "AW1002@14", "AW1002@4"], found);
    }

    [Fact]
    public async Task A_reason_after_the_attributes_counts_and_an_empty_reason_does_not()
    {
        var found = await AnalyzerHarness.Run(new NullForgivingReasonAnalyzer(), "Aw.Probe", """
            namespace Aw.Probe;
            internal sealed class C
            {
                [System.Obsolete("probe")]
                // why: set by Init before any read
                internal string A { get; set; } = null!;
                internal string B { get; set; } = null!; // why:
            }
            """);

        // Line 7: the marker is right but the reason after it is empty.
        Assert.Equal(["AW1002@7"], found);
    }

    [Fact]
    public async Task A_reason_separated_by_a_blank_line_is_not_directly_above()
    {
        var found = await AnalyzerHarness.Run(new NullForgivingReasonAnalyzer(), "Aw.Probe", """
            namespace Aw.Probe;
            internal static class C
            {
                internal static string M(string? s)
                {
                    // why: this comment is about something else

                    var y = s!;
                    // why: two comment lines directly above count
                    // (the reason may run on)
                    var z = s!;
                    return y + z;
                }
            }
            """);

        Assert.Equal(["AW1002@8"], found);
    }

    [Fact]
    public async Task Why_in_a_string_literal_is_not_a_comment()
    {
        var found = await AnalyzerHarness.Run(
            new NullForgivingReasonAnalyzer(), "Aw.Probe", "namespace Aw.Probe;\ninternal static class C { internal static string M(string? s) => s! + \"// why: no\"; }\n");

        Assert.Equal(["AW1002@2"], found);
    }
}
