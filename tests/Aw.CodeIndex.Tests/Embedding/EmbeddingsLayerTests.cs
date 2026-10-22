using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class EmbeddingsLayerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static BuildHarness Arranged(int methods, FakeEmbedder? embedder = null, CodeIndexSettings? settings = null)
    {
        var harness = new BuildHarness { Embedder = embedder ?? new FakeEmbedder(), Settings = settings ?? CodeIndexSettings.Default };
        var symbols = new List<ParsedSymbol> { Parsed.Class("A", 1) };
        symbols.AddRange(Enumerable.Range(0, methods).Select(i => Parsed.Method("A.M" + Number(i), 2 + (i * 3)) with { Body = "{ Work(" + Number(i) + "); }" }));
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(symbols));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A").WithFile(BuildHarness.Second, "src/A.cs", "class A");
        return harness;
    }

    private static string Number(int i) => i.ToString("D4", CultureInfo.InvariantCulture);

    private static async Task<LayerRecord> Layer(BuildHarness harness)
        => (await harness.Header())!.Layers.Single(l => l.Layer == LayerName.Embeddings);

    [Fact]
    public async Task A_build_embeds_each_method_as_its_name_signature_and_body_and_records_the_layer_ok()
    {
        var embedder = new FakeEmbedder();
        var harness = Arranged(2, embedder);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(["M0000\nvoid M0000()\n{ Work(0000); }", "M0001\nvoid M0001()\n{ Work(0001); }"], Assert.Single(embedder.Batches), StringComparer.Ordinal);
        Assert.Equal((2, 2), (report.Embedded, report.Embeddable));
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Ok, layer.State);
        Assert.Null(layer.Code);
        Assert.Equal("2 of 2 embedded", layer.Detail);
        Assert.Equal(FakeEmbedder.DefaultModel, layer.Stamp);
        Assert.Equal(LayerState.Ok, report.Layers.Single(l => l.Layer == LayerName.Embeddings).State);
    }

    [Fact]
    public async Task Texts_go_to_the_embedder_in_batches_of_32()
    {
        var embedder = new FakeEmbedder();

        _ = await Arranged(70, embedder).Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal([32, 32, 6], embedder.Batches.Select(b => b.Count));
    }

    [Fact]
    public async Task A_build_embeds_at_most_4096_symbols_and_the_next_build_finishes_the_rest()
    {
        var harness = Arranged(4_100);

        var first = await harness.Build(BuildHarness.First, cancellationToken: Token);
        var pending = await Layer(harness);
        var second = await harness.Build(BuildHarness.Second, cancellationToken: Token);
        var done = await Layer(harness);

        Assert.Equal((4_096, 4_100), (first.Embedded, first.Embeddable));
        Assert.Equal(LayerState.Pending, pending.State);
        Assert.Equal("4096 of 4100 embedded", pending.Detail);
        Assert.Equal((4_100, 4_100), (second.Embedded, second.Embeddable));
        Assert.Equal(LayerState.Ok, done.State);
        Assert.Equal([4], harness.Embedder.Batches.Skip(128).Select(b => b.Count));
    }

    [Fact]
    public async Task A_server_that_is_down_makes_the_layer_unavailable_with_its_code_and_the_build_still_publishes()
    {
        var embedder = new FakeEmbedder { FailProbe = CodeIndexErrors.EmbeddingServerFailed.With("down") };
        var harness = Arranged(3, embedder);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, layer.Code);
        Assert.Equal((0, 3), (report.Embedded, report.Embeddable));
        Assert.Empty(embedder.Batches);
        Assert.Equal(LayerState.Ok, (await harness.Header())!.Layers.Single(l => l.Layer == LayerName.Structure).State);
    }

    [Fact]
    public async Task A_model_that_is_not_the_pinned_one_makes_the_layer_unavailable_with_the_pin_code()
    {
        var embedder = new FakeEmbedder { FailProbe = CodeIndexErrors.PinMismatch.With("digest differs") };

        var harness = Arranged(1, embedder);

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, (await Layer(harness)).Code);
    }

    [Fact]
    public async Task A_refused_url_makes_the_layer_unavailable_with_the_target_code()
    {
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: true, "http://localhost:11434", Model: null) };
        var harness = Arranged(1, settings: settings);

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        var layer = await Layer(harness);

        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingTargetRefused.Code, layer.Code);
    }

    [Fact]
    public async Task A_server_that_fails_part_way_keeps_the_vectors_made_and_marks_the_layer_unavailable()
    {
        var embedder = new FakeEmbedder { FailEmbedAfter = (1, CodeIndexErrors.EmbeddingServerFailed.With("went away")) };
        var harness = Arranged(70, embedder);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        Assert.Equal((32, 70), (report.Embedded, report.Embeddable));
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, layer.Code);
        Assert.Equal("32 of 70 embedded", layer.Detail);
    }

    [Fact]
    public async Task A_layer_the_profile_turned_off_is_disabled_and_nothing_is_sent_or_kept()
    {
        var embedder = new FakeEmbedder();
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: false, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null) };
        var harness = Arranged(2, embedder, settings);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(LayerState.Disabled, (await Layer(harness)).State);
        Assert.Equal(0, embedder.Probes);
        Assert.Empty(embedder.Batches);
        Assert.Equal(0, report.Embedded);
        await using var build = (await harness.Store.BeginBuild(BuildHarness.Name, incremental: true, Token)).Value;
        Assert.Empty((await build.EmbedCandidates(10, Token)).Value);
    }

    [Fact]
    public async Task The_settings_model_is_the_layers_stamp_and_the_vectors_model()
    {
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, "my-model:3") };
        var harness = Arranged(1, settings: settings);

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal("my-model:3", (await Layer(harness)).Stamp);
    }

    [Fact]
    public async Task Progress_lines_carry_counts_only()
    {
        var lines = new Lines();

        _ = await Arranged(40).Build(BuildHarness.First, lines, Token);

        Assert.Contains("embeddings 32 of 40", lines.All, StringComparer.Ordinal);
        Assert.Contains("embeddings 40 of 40", lines.All, StringComparer.Ordinal);
        Assert.DoesNotContain(lines.All, l => l.Contains("Work(", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancel_between_batches_leaves_the_live_index_alone()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var embedder = new FakeEmbedder();
        var harness = Arranged(70, embedder);
        var lines = new CancelAfterEmbedding(cancel);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Build(BuildHarness.First, lines, cancel.Token));

        Assert.Null(await harness.Header());
    }

    [Fact]
    public async Task A_secret_in_a_body_is_scrubbed_and_cut_before_it_is_stored_and_embedded()
    {
        var embedder = new FakeEmbedder();
        var harness = new BuildHarness { Embedder = embedder, Scrubber = new FakeBodyScrubber("hunter2") };
        var leaky = Parsed.Method("A.Run", 2) with { Body = "{ Login(\"hunter2\"); " + new string('x', 3_000) + " }" };
        _ = harness.Parser.With("src/A.cs", new ParseOutcome([leaky]));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        var text = Assert.Single(Assert.Single(embedder.Batches));
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.True(text.Length <= CodeIndexRules.EmbedTextMaxCharacters);
    }

    private sealed class CancelAfterEmbedding(CancellationTokenSource cancel) : IProgress<string>
    {
        public void Report(string value)
        {
            if (value.StartsWith("embeddings", StringComparison.Ordinal))
            {
                cancel.Cancel();
            }
        }
    }
}
