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
