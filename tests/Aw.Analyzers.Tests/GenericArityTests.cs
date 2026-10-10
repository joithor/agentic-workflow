using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class GenericArityTests
{
    private const string Sample = """
        namespace Aw.Probe;
        internal sealed class Three<T1, T2, T3> { internal T1? A; internal T2? B; internal T3? C; }
        internal sealed class Four<T1, T2, T3, T4> { internal T1? A; internal T2? B; internal T3? C; internal T4? D; }
        internal static class M
        {
            internal static string Ok<T1, T2, T3>(T1 a, T2 b, T3 c) => $"{a}{b}{c}";
            internal static string Wide<T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d) => $"{a}{b}{c}{d}";
        }
        internal delegate void Callback<T1, T2, T3, T4>(T1 a, T2 b, T3 c, T4 d);
        internal abstract class Base { internal abstract void Go<T1, T2, T3, T4>(); }
        internal sealed class Derived : Base { internal override void Go<T1, T2, T3, T4>() { } }
        internal interface IWide { void Run<T1, T2, T3, T4>(); }
        internal sealed class Wide : IWide
        {
            public void Run<T1, T2, T3, T4>() { }
            internal static int Local() { return L<int, int, int, int>(); static int L<T1, T2, T3, T4>() => 1; }
        }
        """;

    [Fact]
    public async Task Flags_types_methods_and_delegates_over_three_type_parameters()
    {
        var found = await AnalyzerHarness.Run(new GenericArityAnalyzer(), "Aw.Probe", Sample);

        // The override (line 11) and the interface implementation (line 15) repeat their base's shape; the base
        // declarations (lines 10, 12) are where it's fixed. Local functions count too (line 16).
        Assert.Equal(["AW1009@10", "AW1009@12", "AW1009@16", "AW1009@3", "AW1009@7", "AW1009@9"], found);
    }
}
