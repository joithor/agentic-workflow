using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class RawIdTests
{
    private const string Sample = """
        using System;
        using System.Collections.Generic;
        namespace Aw.Probe.Core;
        [Vogen.ValueObject<Guid>] public readonly partial record struct ItemId(Guid Value) { public static ItemId From(Guid value) => new(value); }
        public sealed record StartItem(string ItemId, string Name);
        public interface IItems
        {
            void Get(Guid run);
            string NextId();
            IReadOnlyList<string> RunIds { get; }
            void Fine(ItemId item, string name, int count);
        }
        internal sealed class Hidden
        {
            public void Get(Guid run) { }
        }
        """;

    private const string VogenStub =
        "namespace Vogen;\n[System.AttributeUsage(System.AttributeTargets.Struct)]\npublic sealed class ValueObjectAttribute<T> : System.Attribute { }\n";

    [Fact]
    public async Task Flags_raw_string_and_Guid_ids_in_public_Core_signatures()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", ("Sample.cs", Sample), ("Vogen.cs", VogenStub));

        // Line 4: a typed id's own members (Value, From) wrap the primitive; they are the id, not a raw one.
        Assert.Equal(["AW1001@10", "AW1001@5", "AW1001@8", "AW1001@9"], found);
    }

    [Fact]
    public async Task Finds_a_Guid_inside_any_type_argument_and_in_delegates_and_reports_overrides_at_the_base()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", """
            using System;
            using System.Collections.Generic;
            namespace Aw.Probe.Core;
            public abstract class Store
            {
                public abstract IReadOnlyDictionary<Guid, int> Map { get; }
                public abstract (Guid Run, int N) Pair();
            }
            public sealed class Concrete : Store
            {
                public override IReadOnlyDictionary<Guid, int> Map => new Dictionary<Guid, int>();
                public override (Guid Run, int N) Pair() => default;
            }
            public delegate void Handler(Guid run);
            public delegate Guid NextRun();
            """);

        // Lines 11-12: overrides repeat the base's shape and are reported there (lines 6-7). Line 15: a delegate's
        // return type counts too.
        Assert.Equal(["AW1001@14", "AW1001@15", "AW1001@6", "AW1001@7"], found);
    }

    [Fact]
    public async Task Reports_an_implementation_when_its_base_is_not_reported()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", """
            using System;
            namespace Aw.Probe.Core;
            public interface IRead<TId> { void Find(TId id); }
            public sealed class Reader : IRead<Guid> { public void Find(Guid id) { } }
            internal interface IRepo { void Get(Guid run); }
            public sealed class Repo : IRepo { public void Get(Guid run) { } }
            public sealed class Key : IEquatable<Guid> { public bool Equals(Guid other) => false; }
            public interface IRuns { void Stop(Guid run); }
            public sealed class Runs : IRuns { public void Stop(Guid run) { } }
            public interface IFind { void Find(Guid run, string name); }
            public sealed class Finder : IFind { public void Find(Guid run, string ownerId) { } }
            """);

        // Line 4: the generic base declares TId, not Guid. Line 6: the internal interface isn't public surface.
        // Line 7: the base is the BCL's. Line 8 is reported at the base, so its implementation (line 9) isn't.
        // Line 11: the base reports 'run' (line 10), but 'ownerId' is raw only in the implementation.
        Assert.Equal(["AW1001@10", "AW1001@11", "AW1001@4", "AW1001@6", "AW1001@7", "AW1001@8"], found);
    }

    [Fact]
    public async Task Checks_events_and_indexer_parameters()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", """
            using System;
            namespace Aw.Probe.Core;
            public sealed class Bus
            {
                public event Action<Guid>? Changed;
                public int this[Guid run] => Changed is null ? 0 : 1;
            }
            """);

        Assert.Equal(["AW1001@5", "AW1001@6"], found);
    }

    [Fact]
    public async Task Checks_extension_receivers_operators_conversions_and_type_arguments_an_implementation_adds()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", """
            using System;
            using System.Collections.Generic;
            namespace Aw.Probe.Core;
            public static class Ext
            {
                extension(Guid run) { public bool IsNew() => false; }
                extension(string ownerId) { public bool IsOwner() => ownerId.Length > 0; }
            }
            public sealed class W { public static W operator +(W a, Guid b) => a; public static explicit operator Guid(W w) => default; }
            public interface IMap<T> { IReadOnlyDictionary<Guid, T> Map(); }
            public sealed class M2 : IMap<Guid> { public IReadOnlyDictionary<Guid, Guid> Map() => new Dictionary<Guid, Guid>(); }
            public sealed class M3 : IMap<int> { public IReadOnlyDictionary<Guid, int> Map() => new Dictionary<Guid, int>(); }
            """);

        // Lines 6-7: each extension block's receiver, once. Line 9: the operator's parameter and the conversion's
        // result. Line 11: T = Guid adds a raw id the base (line 10) doesn't have; M3 (line 12) adds none.
        Assert.Equal(["AW1001@10", "AW1001@11", "AW1001@6", "AW1001@7", "AW1001@9", "AW1001@9"], found);
    }

    [Fact]
    public async Task A_partial_member_is_reported_once()
    {
        var found = await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Core", """
            namespace Aw.Probe.Core;
            public sealed partial class P
            {
                public partial void M(string ownerId);
                public partial void M(string ownerId) { }
            }
            """);

        Assert.Equal(["AW1001@4"], found);
    }

    [Fact]
    public async Task Applies_to_Features_assemblies()
    {
        var found = await AnalyzerHarness.Run(
            new RawIdAnalyzer(), "Aw.Probe.Features", "namespace Aw.Probe.Features.Start;\npublic sealed record StartRequest(System.Guid Run);\n");

        Assert.Equal(["AW1001@2"], found);
    }

    [Fact]
    public async Task Ignores_other_projects()
    {
        Assert.Empty(await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Probe.Infrastructure", ("Sample.cs", Sample), ("Vogen.cs", VogenStub)));
        Assert.Empty(await AnalyzerHarness.Run(new RawIdAnalyzer(), "Aw.Kernel", ("Sample.cs", Sample), ("Vogen.cs", VogenStub)));
    }

    [Fact]
    public async Task Message_names_the_member_the_type_and_the_fix()
    {
        var diagnostics = await AnalyzerHarness.Diagnostics(
            new RawIdAnalyzer(), "Aw.Probe.Core", ("Sample.cs", "namespace Aw.Probe.Core;\npublic interface IRuns { void Get(System.Guid runId); }\n"));

        var message = Assert.Single(diagnostics).GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("'runId'", message, StringComparison.Ordinal);
        Assert.Contains("raw Guid id", message, StringComparison.Ordinal);
        Assert.Contains("Aw.Probe.Core", message, StringComparison.Ordinal);
        Assert.Contains("typed id", message, StringComparison.Ordinal);
    }
}
