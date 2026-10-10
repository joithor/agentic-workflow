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
        """;

    [Fact]
    public async Task Flags_types_methods_and_delegates_over_three_type_parameters()
    {
        var found = await AnalyzerHarness.Run(new GenericArityAnalyzer(), "Aw.Probe", Sample);

        Assert.Equal(["AW1009@3", "AW1009@6", "AW1009@8"], found);
    }
}
