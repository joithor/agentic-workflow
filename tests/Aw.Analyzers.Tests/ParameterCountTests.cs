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

    [Fact]
    public async Task A_partial_method_is_reported_once()
    {
        var found = await AnalyzerHarness.Run(new ParameterCountAnalyzer(), "Aw.Probe", """
            namespace Aw.Probe;
            internal sealed partial class P
            {
                internal partial void M(int a, int b, int c, int d, int e, int f);
                internal partial void M(int a, int b, int c, int d, int e, int f) { }
            }
            internal partial struct S
            {
                internal partial S(int a, int b, int c, int d, int e, int f);
                internal partial S(int a, int b, int c, int d, int e, int f) { }
            }
            """);

        // A partial constructor reaches the analyzer only as its implementing part (line 10).
        Assert.Equal(["AW1006@10", "AW1006@4"], found);
    }

    private const string Constructors = """
        namespace Aw.Probe;
        internal readonly struct Point(int a, int b, int c, int d, int e, int f) { internal int S => a + b + c + d + e + f; }
        internal readonly struct Pair
        {
            internal Pair(int a, int b, int c, int d, int e, int f) { A = a + b + c + d + e + f; }
            internal int A { get; }
        }
        internal sealed record Row(int A, int B, int C, int D, int E, int F)
        {
            internal Row(int a, int b, int c, int d, int e, int f, int g) : this(a, b, c, d, e, f + g) { }
        }
        internal readonly record struct Cell(int A, int B, int C, int D, int E, int F);
        internal sealed class Service(int a, int b, int c, int d, int e, int f) { internal int S => a + b + c + d + e + f; }
        """;

    [Fact]
    public async Task Counts_struct_constructors_but_not_record_or_class_constructors()
    {
        var found = await AnalyzerHarness.Run(new ParameterCountAnalyzer(), "Aw.Probe", Constructors);

        // Struct constructors, primary (line 2) or not (line 5), are AW1006's. A record's constructors, positional
        // (lines 8, 12) or explicit (line 10), declare data; class constructors (line 13) are AW1007's.
        Assert.Equal(["AW1006@2", "AW1006@5"], found);
    }
}
