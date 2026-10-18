using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the context routes answer as the others do, fake and client alike.</summary>
public abstract partial class SindriApiContract
{
    private static string[] AnchorKeys(string scenario) => [.. GoldenFiles.Node("context-lenses", scenario)["anchors"]!.AsArray().Select(a => (string)a!["key"]!)];

    [Fact]
    public async Task Lenses_answers_the_golden_for_a_known_anchor_set()
    {
        var api = Reach(new FakeSindriApi());
        var one = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

        var single = (await api.Lenses(_api, new LensesQuery([one], Depth: null, Draft: false, MapVersion: null), Token)).Value;
        var set = (await api.Lenses(_api, new LensesQuery(AnchorKeys("set-two-anchors"), Depth: null, Draft: false, MapVersion: null), Token)).Value;

        Expect.Sequence([one], single.Anchors.Select(a => a.Key));
        Expect.Sequence(AnchorKeys("set-two-anchors"), set.Anchors.Select(a => a.Key));
        Expect.Equal((1, 2), (single.SchemaVersion, set.SchemaVersion));
    }

    [Fact]
    public async Task Lenses_answers_003_for_an_unknown_anchor()
    {
        var api = Reach(new FakeSindriApi());

        var lenses = await api.Lenses(_api, new LensesQuery(["feature:tabs.nope"], Depth: null, Draft: false, MapVersion: null), Token);

        var served = GoldenFiles.Node("context-lenses", "unknown-anchor-003");
        Expect.Equal(((string)served["title"]!, (string)served["detail"]!), (lenses.Error.Code, lenses.Error.Message));
    }

    [Fact]
    public async Task ReviewDecision_applies_a_decision_to_the_queue()
    {
        var api = Reach(new FakeSindriApi());
        var queue = GoldenFiles.Node("context-map-review", "map-tabs-draft");
        var entry = queue["entries"]![0]!;

        var accepted = (await api.ReviewDecision(_api, new ReviewDecisionRequest("accept", Node: (string)entry["node"]!, Link: (string?)entry["link"]), Token)).Value;
        var after = (await api.ReviewQueue(_api, limit: null, Token)).Value;

        var written = GoldenFiles.Node("context-map-review", "post-accept-link")["result"]!["written"]!.AsArray().Select(w => (string)w!);
        Expect.Sequence(written, accepted.Result!.Written);
        Expect.Equal((queue["entries"]!.AsArray().Count - 1, (int)queue["undecided"]! - 1), (after.Entries.Count, after.Undecided));
        Expect.True(after.Entries.All(e => e.Node != (string)entry["node"]!));

        _ = await api.ReviewDecision(_api, new ReviewDecisionRequest("undo", Overrides: [.. accepted.Result.Written]), Token);
        var undone = (await api.ReviewQueue(_api, limit: null, Token)).Value;

        Expect.Equal((queue["entries"]!.AsArray().Count, (int)queue["undecided"]!), (undone.Entries.Count, undone.Undecided));
    }

    [Fact]
    public async Task FileMap_lists_children_of_a_path()
    {
        var api = Reach(new FakeSindriApi());
        var root = GoldenFiles.Node("context-filemap", "root");
        var tabs = GoldenFiles.Node("context-filemap", "src-tabs");
        var unmapped = GoldenFiles.Node("context-filemap", "src-unmapped");
        var repo = (string)root["repo"]!;

        var top = (await api.FileMap(_api, new FileMapQuery(repo, Path: null, UnmappedOnly: false, Draft: false), Token)).Value;
        var children = (await api.FileMap(_api, new FileMapQuery(repo, Path: (string)tabs["path"]!, UnmappedOnly: false, Draft: false), Token)).Value;
        var only = (await api.FileMap(_api, new FileMapQuery(repo, Path: (string)unmapped["path"]!, UnmappedOnly: true, Draft: false), Token)).Value;

        Expect.Sequence(Names(root), top.Entries.Select(e => e.Name));
        Expect.Sequence(Names(tabs), children.Entries.Select(e => e.Name));
        Expect.Sequence(Names(unmapped), only.Entries.Select(e => e.Name));
    }

    private static string[] Names(JsonNode map) => [.. map["entries"]!.AsArray().Select(e => (string)e!["name"]!)];
}
