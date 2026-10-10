using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class ConstructorDependencyTests
{
    private const string Sample = """
        namespace Aw.Probe;
        internal sealed class Five(int a, int b, int c, int d, int e) { internal int S => a + b + c + d + e; }
        internal sealed class Six(int a, int b, int c, int d, int e, int f) { internal int S => a + b + c + d + e + f; }
        internal sealed class Eight(int a, int b, int c, int d, int e, int f, int g, int h) { internal int S => a + b + c + d + e + f + g + h; }
        internal sealed class Explicit
        {
            private readonly int _s;
            internal Explicit(int a, int b, int c, int d, int e, int f, int g) { _s = a + b + c + d + e + f + g; }
            internal int S => _s;
        }
        internal sealed record Wide(int A, int B, int C, int D, int E, int F, int G, int H);
        """;

    [Fact]
    public async Task Split_signal_over_five_and_error_over_seven()
    {
        var found = await AnalyzerHarness.Run(new ConstructorDependencyAnalyzer(), "Aw.Probe", Sample);

        // Records are data shapes, not services with dependencies (line 11).
        Assert.Equal(["AW1007@4", "AW1008@3", "AW1008@8"], found);
    }
}
