using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class ParameterCountTests
{
    private const string Sample = """
        namespace Aw.Probe;
        internal interface IWide { void Wide(int a, int b, int c, int d, int e, int f); }
        internal abstract class Base { internal abstract void Over(int a, int b, int c, int d, int e, int f); }
        internal sealed class C(int a, int b, int c, int d, int e, int f) : Base, IWide
        {
            internal static void Five(int a, int b, int c, int d, int e) { }
            internal static void Six(int a, int b, int c, int d, int e, int f) { }
            internal override void Over(int a, int b, int c, int d, int e, int f) { }
            public void Wide(int a, int b, int c, int d, int e, int f) { }
            internal int this[int a, int b, int c, int d, int e, int f] => a;
            internal int Local()
            {
                return L(1, 2, 3, 4, 5, 6);
                static int L(int a, int b, int c, int d, int e, int f) => a;
            }
            internal int Sum => a + b + c + d + e + f;
        }
        internal delegate void Handler(int a, int b, int c, int d, int e, int f);
        internal sealed record Row(int A, int B, int C, int D, int E, int F);
        """;

    [Fact]
    public async Task Flags_methods_indexers_local_functions_and_delegates_over_five()
    {
        var found = await AnalyzerHarness.Run(new ParameterCountAnalyzer(), "Aw.Probe", Sample);

        // Not flagged: the override (line 8) and the interface implementation (line 9), whose shape the base sets;
        // constructors (AW1007 owns them, line 4) and record positional parameters (line 19, data, not a call).
        Assert.Equal(["AW1006@10", "AW1006@14", "AW1006@18", "AW1006@2", "AW1006@3", "AW1006@7"], found);
    }
}
