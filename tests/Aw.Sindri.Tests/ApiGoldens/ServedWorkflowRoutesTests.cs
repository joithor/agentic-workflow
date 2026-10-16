using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The workflow view routes as the running host serves them (API spec section 3.8, API P11): a served instance, list, fact
/// and recipe read back into the section 3.8 records, so the bodies the UI builds on are the ones the routes write.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ServedWorkflowRoutesTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    /// <summary>The goldens this class records from the served routes (the rest of 'Golden/Api/workflow-*' is the fixtures' in <see cref="WorkflowViewGoldenTests"/>).</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Goldens { get; } = [("workflow-instance", "not-found"), ("workflow-instance-fact", "not-found")];

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Get_instance_serves_InstanceDetail_that_reads_back_into_the_record()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var body = await Read(served, "/sindri/workflow/instances/" + id);

        var options = served.Sindri.GetRequiredService<JsonSerializerOptions>();
        var detail = JsonSerializer.Deserialize<InstanceDetail>(body, options)!;
        Assert.Equal("T-1/step/1", detail.Address.Value);
        Assert.NotEmpty(detail.Current.Status.Text);
        Assert.Equal(ApiGolden.Reindent(body), ApiGolden.Reindent(JsonSerializer.Serialize(detail, options)));
    }

    [Fact(Timeout = 120_000)]
    public async Task List_instances_serves_InstanceList_with_counts_over_the_root_set()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        _ = await Started(served, "T-1");
        _ = await Started(served, "T-2");
        _ = await served.Cli("cancel", "T-2/step/1");
        _ = await served.Until("T-2/step/1", static v => v.State == "cancelled");

        var list = await ListUntil(served, "/sindri/workflow/instances", static l => l.Instances.Count == 2);

        Assert.Equal(new ListCounts(Open: 1, NeedsYou: 1, WithYou: 0, Running: 0, Done: 1), list.Counts);
        Assert.Null(list.Empty);
    }

    [Fact(Timeout = 120_000)]
    public async Task List_instances_open_true_keeps_only_unfinished_rows()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        _ = await Started(served, "T-1");
        _ = await Started(served, "T-2");
        _ = await served.Cli("cancel", "T-2/step/1");
        _ = await served.Until("T-2/step/1", static v => v.State == "cancelled");

        var list = await ListUntil(served, "/sindri/workflow/instances?open=true", static l => l.Instances.Count == 1);

        Assert.Equal(["T-1/step/1"], list.Instances.Select(i => i.Address.Value), StringComparer.Ordinal);
        Assert.Equal(1, list.Counts.Done);   // the counts cover the root set before the open filter
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_instance_fact_serves_FactView_and_an_undeclared_key_is_SND_ITEM_404()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var fact = await Read(served, $"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:spec")}");
        var missing = await served.Http.Ask(Get($"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:unknown")}", Human(Token(served))));

        var view = JsonSerializer.Deserialize<FactView>(fact, served.Sindri.GetRequiredService<JsonSerializerOptions>())!;
        Assert.Equal(("fact:spec", FactType.Text, "the spec", false), (view.Key, view.Type, view.Body.GetString(), view.Truncated));
        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (missing.Status, missing.Code));
    }

    [Fact(Timeout = 120_000)]
    public async Task An_unknown_instance_answers_the_pinned_not_found_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var unknown = await served.Http.Ask(Get("/sindri/workflow/instances/WFI-" + new string('a', 32), Human(Token(served))));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (unknown.Status, unknown.Code));
        ApiGolden.Match(unknown.Body, _harness.Home, "workflow-instance", "not-found");
    }

    [Fact(Timeout = 120_000)]
    public async Task An_undeclared_fact_answers_the_pinned_not_found_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var id = await Started(served, "T-1");

        var undeclared = await served.Http.Ask(Get($"/sindri/workflow/instances/{id}/facts/{Uri.EscapeDataString("fact:unknown")}", Human(Token(served))));

        Assert.Equal((HttpStatusCode.NotFound, "SND-ITEM-404"), (undeclared.Status, undeclared.Code));
        ApiGolden.Match(undeclared.Body, _harness.Home, "workflow-instance-fact", "not-found");
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_recipe_by_id_serves_RecipeView_with_outputs_and_every_input_kind()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/desk-round-trip.yaml"));
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);

        var body = await Read(served, "/sindri/workflow/recipes/workflow.dev-desk-round-trip");

        var view = JsonSerializer.Deserialize<RecipeView>(body, served.Sindri.GetRequiredService<JsonSerializerOptions>())!;
        Assert.Equal("workflow.dev-desk-round-trip", view.Id.Value);
        Assert.Contains(view.Outputs, o => o.Key == "fact:output");
        Assert.All(view.Inputs, i => Assert.False(string.IsNullOrEmpty(i.Kind)));
    }

    [Fact(Timeout = 120_000)]
    public async Task Get_recipe_by_id_at_hash_serves_the_same_body()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var byId = await Read(served, "/sindri/workflow/recipes/workflow.step");
        var hash = JsonDocument.Parse(byId).RootElement.GetProperty("hash").GetString()!;

        var byHash = await Read(served, "/sindri/workflow/recipes/workflow.step@" + hash.Replace("sha256:", string.Empty, StringComparison.Ordinal));

        Assert.Equal(byId, byHash);
    }

    [Fact(Timeout = 120_000)]
    public async Task Dev_triage_and_desk_round_trip_serve_the_golden_shape()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/desk-round-trip.yaml"));
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/triage-three-state.yaml"));
        await using var served = await Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        foreach (var (recipe, item, golden, facts) in new[]
        {
            ("workflow.dev-desk-round-trip", "T-3", "dev-desk-round-trip", new[] { "fact:request" }),
            ("workflow.dev-triage-three-state", "T-4", "dev-triage-three-state", ["fact:scope", "fact:item"]),
        })
        {
            var started = await served.Cli(["start", recipe, "--item", item, .. facts.SelectMany(f => new[] { "--fact", f + "=" + _harness.File(item + f[5..] + ".md", f == "fact:item" ? $"{{ \"id\": \"{item}\", \"repo\": \"app\", \"spec\": \"the spec\" }}" : "text of " + f) })]);
            Assert.True(started.Exit == 0, started.Stderr);
            var id = await IdOfItem(served, item);

            var served1 = JsonDocument.Parse(await Read(served, "/sindri/workflow/instances/" + id)).RootElement;

            var expected = JsonDocument.Parse(await File.ReadAllTextAsync(GoldenPath(golden), TestContext.Current.CancellationToken)).RootElement;
            Assert.Equal(Keys(expected), Keys(served1));
            Assert.Equal(Cells(expected), Cells(served1));
            Assert.Equal(FactStatuses(expected), FactStatuses(served1));
        }
    }

    private static string GoldenPath(string scenario)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, "tests", "Aw.Sindri.Tests", "Golden", "Api", "workflow-instance", scenario + ".json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("no golden " + scenario);
    }

    private static string[] Keys(JsonElement e) => [.. e.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal)];

    private static string[] Cells(JsonElement e) => [.. e.GetProperty("states").EnumerateArray().Select(static s =>
        $"{s.GetProperty("order")}/{(s.TryGetProperty("loops", out var l) ? string.Join(',', l.EnumerateArray().Select(static p => $"{p.GetProperty("with")}:{p.GetProperty("max")}")) : "-")}/{(s.TryGetProperty("desk", out var d) ? d.ValueKind.ToString() : "-")}")];

    private static string[] FactStatuses(JsonElement e) => [.. e.GetProperty("facts").EnumerateArray().Select(static f => $"{f.GetProperty("key")}={f.GetProperty("status")}")];

    private static string Token(Served served) => served.Sindri.GetRequiredService<Core.Auth.HeldHumanToken>().Current!.Value;

    private async Task<string> Started(Served served, string item)
    {
        var run = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File(item + "-spec.md", "the spec"));
        Assert.True(run.Exit == 0, run.Stderr);
        _ = await served.Until(item + "/step/1", static v => v.Task is not null);
        return await IdOfItem(served, item);
    }

    private static async Task<string> IdOfItem(Served served, string item)
    {
        var body = await Read(served, $"/sindri/workflow/instances?address={Uri.EscapeDataString(item)}");
        return JsonDocument.Parse(body).RootElement.GetProperty("instances")[0].GetProperty("id").GetString()!;
    }

    private static async Task<string> Read(Served served, string path)
    {
        var answer = await served.Http.Ask(Get(path, Human(Token(served))));
        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
        return answer.Body;
    }

    private static async Task<InstanceList> ListUntil(Served served, string path, Func<InstanceList, bool> holds)
    {
        var options = served.Sindri.GetRequiredService<JsonSerializerOptions>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            var list = JsonSerializer.Deserialize<InstanceList>(await Read(served, path), options)!;   // visibility is eventually consistent
            if (holds(list) || DateTime.UtcNow > deadline)
            {
                return list;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }
}
