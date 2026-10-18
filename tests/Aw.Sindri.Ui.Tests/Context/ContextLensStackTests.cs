using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The lens stack (U9a): the anchor box reads the lenses and draws the head, bands, edges, coverage and searched sources. Every value is read from a context-lenses golden.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLensStackTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static System.Text.Json.Nodes.JsonNode Golden(string scenario) => GoldenFiles.Node("context-lenses", scenario);

    [Fact]
    public async Task One_anchor_reads_the_map_version_and_the_anchor_count_into_the_head()
    {
        var golden = Golden("map-tabs");
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        Assert.Equal(Labels.ContextHead((string)golden["mapVersion"]!, 1), page.Head);
        Assert.True(page.HasHead);
    }

    [Fact]
    public async Task Two_anchors_read_the_count_of_the_set()
    {
        var golden = Golden("set-two-anchors");
        var page = Page(new FakeSindriApi());
        page.AnchorText = string.Join(' ', golden["anchors"]!.AsArray().Select(a => (string)a!["key"]!));

        await page.Submit(Token);

        Assert.Equal(Labels.ContextHead((string)golden["mapVersion"]!, golden["anchors"]!.AsArray().Count), page.Head);
    }

    [Fact]
    public async Task The_bands_edges_coverage_and_sources_are_drawn_from_the_answer()
    {
        var golden = Golden("map-tabs");
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var product = golden["bands"]!["product"]!;
        var coverage = golden["coverage"]!;
        Assert.Equal(3, page.Bands.Count);
        Assert.Equal(product["own"]!.AsArray().Count, page.Bands[1].Lines.Count);
        Assert.Equal(golden["bands"]!["intent"]!["records"]!.AsArray().Count + golden["bands"]!["intent"]!["decisions"]!.AsArray().Count + golden["conflicts"]!.AsArray().Count, page.Bands[0].Lines.Count);
        Assert.Equal(product["edges"]!.AsArray().Count + golden["edges"]!.AsArray().Count, page.Edges.Count);
        Assert.Equal(Labels.ContextCoverIntent((int)coverage["intent"]!["covered"]!, (int)coverage["intent"]!["of"]!), page.Coverage[0].Text);
        Assert.Equal(Labels.ContextCoverCode((int)coverage["code"]!["implemented"]!, (int)coverage["code"]!["of"]!, (int)coverage["code"]!["suggested"]!), page.Coverage[2].Text);
        Assert.Equal(golden["searched"]!["sources"]!.AsArray().Count, page.Searched.Count);
    }

    [Fact]
    public async Task A_model_link_reads_model_suggested_with_its_confidence_and_a_suggested_file_says_so()
    {
        var golden = Golden("map-tabs");
        var edge = golden["edges"]!.AsArray().Single(e => (string)e!["provenance"]! == "model")!;
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var confidence = ((double)edge["confidence"]!).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(page.Edges, l => l.Text.Contains(Labels.ContextModelSuggested + " " + confidence, StringComparison.Ordinal));
        Assert.Contains(page.Bands[2].Lines, l => l.Text.Contains(Labels.ContextSuggestedFile + " " + confidence, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_anchor_is_named_not_found_and_the_rest_still_draw()
    {
        var golden = Golden("set-missing-anchor");
        var page = Page(new FakeSindriApi());
        page.AnchorText = string.Join(' ', golden["anchors"]!.AsArray().Select(a => (string)a!["key"]!).Concat(golden["missingAnchors"]!.AsArray().Select(m => (string)m!)));

        await page.Submit(Token);

        var missing = Assert.Single(page.Missing);
        Assert.Contains((string)golden["missingAnchors"]![0]!, missing.Text, StringComparison.Ordinal);
        Assert.Contains(Labels.ContextNotFound, missing.Text, StringComparison.Ordinal);
        Assert.True(page.HasStack);
    }

    [Fact]
    public async Task An_unknown_anchor_lists_the_served_suggestions_as_choices_and_picking_one_reads_it()
    {
        var golden = Golden("unknown-anchor-003");
        var suggested = golden["suggestedAnchors"]!.AsArray().Select(a => (string)a!).ToList();
        var page = Page(new FakeSindriApi());
        page.AnchorText = "feature:tabs.nope";

        await page.Submit(Token);

        Assert.Equal(suggested, page.Choices);
        Assert.False(page.HasStack);
        Assert.True(page.HasProblem);

        await page.Pick("feature:tabs.tab-groups", Token);
        Assert.Equal("feature:tabs.tab-groups", page.AnchorText);
    }

    [Fact]
    public async Task Repeated_anchors_collapse_and_more_than_twenty_are_refused_without_a_call()
    {
        var server = new FakeSindriApi();
        var page = Page(server);
        var key = (string)Golden("map-tabs")["anchor"]!["key"]!;
        page.AnchorText = $"{key} {key}";
        await page.Submit(Token);
        Assert.Equal(Labels.ContextHead((string)Golden("map-tabs")["mapVersion"]!, 1), page.Head);

        var calls = server.ContextCalls;
        page.AnchorText = string.Join(' ', Enumerable.Range(0, 21).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a:{i}")));
        await page.Submit(Token);

        Assert.Equal(calls, server.ContextCalls);
        Assert.Equal(Labels.ContextTooMany(20), page.Problem);
    }

    [Fact]
    public async Task A_truncated_product_band_counts_its_nodes_not_the_truncation_line()
    {
        var golden = Golden("map-tabs");
        var body = golden.DeepClone();
        body["bands"]!["product"]!["truncated"] = true;
        var server = new FakeSindriApi { LensesAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString()) };
        var page = Page(server);
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var product = golden["bands"]!["product"]!;
        var nodes = product["own"]!.AsArray().Count + product["neighbors"]!.AsArray().Count + product["affects"]!.AsArray().Count;
        Assert.Equal(Labels.ContextBandHeader(Labels.ContextProduct, nodes), page.Bands[1].Header);
        Assert.Equal(nodes + 1, page.Bands[1].Lines.Count);
    }

    [Fact]
    public async Task A_read_in_flight_does_not_overwrite_the_too_many_refusal()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new FakeSindriApi { LensesGate = gate };
        var page = Page(server);
        page.AnchorText = (string)Golden("map-tabs")["anchor"]!["key"]!;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        var first = page.Submit(limit.Token);

        page.AnchorText = string.Join(' ', Enumerable.Range(0, 21).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a:{i}")));
        await page.Submit(limit.Token);
        gate.SetResult();
        await first;

        Assert.Equal(Labels.ContextTooMany(20), page.Problem);
        Assert.False(page.HasStack);
    }
}
