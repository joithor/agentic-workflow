using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.GetInstance;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The layout members of the instance view (UI screens E-20, E-23; API spec 3.8): their JSON names, enum strings, round trip and the list row built from them.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewLayoutTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    private static InstanceView Full()
    {
        var badge = new EngineOwnedBadge(EngineOwnedKind.TakenOver, "taken over by thor");
        var progress = new Progress(4, 9);
        var round = new Round(2, 3);
        var park = new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/1/step.4")), InstanceAddress.From("T-3/step/1/step.4"), "parked", Reason: null);
        var row = new ChildRow(WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/2")), InstanceAddress.From("T-3/step/2"), ItemKey.From("ITM-1"), "Sync pill", StateGlyph.Active, "produce", new StateChip("RUNNING", ChipTone.Ok), round, Agent: null, 360, badge, new EngineOwnedBadge(EngineOwnedKind.Parked, "parked"), [ActionId.From("OpenItem")]);
        return StatusCases.View("desk-task") with
        {
            ItemId = ItemKey.From("ITM-1"),
            Urgency = RecipeUrgency.Incident,
            EngineOwned = badge,
            Subject = new SubjectRef("linear-project", "APP", "APP Offline sync", Href: null),
            Inputs = [new InputPresence("spec", "Spec", "file", Required: true, Present: false)],
            OrderedBy = PathOrdering.TransitionList,
            States =
            [
                new StateCell("build", Order: null, BranchOf: "intake", StateGlyph.Pending, badge, round, [new LoopPair("verify", 1, 3)], Desk: true, Child: true, progress, Outcome: null, Terminal: false),
            ],
            Status = new StatusFields(
                new StatusHeader(InstanceAddress.From("T-3/step/1"), RecipeId.From("workflow.step"), "0123456789ab", 4, "produce", "desk task", StatusCases.Now, 4320),
                "next text",
                new StatusAnswer(AnswerKind.GrantTokens, ActionId.From("ApplyCommand:answer"), ActionClass.Protected, "sindri workflow answer", AtTerminal: true),
                park,
                new StatusDrift(BlockId.From("step.produce"), NeedsReapproval: true),
                [new StatusInput("spec", "spec.md", Hash: null)],
                new StatusTimeout(TimeoutKind.Parks, 1000, "desk"),
                new StatusBudget("state", 1, Limit: null, new BudgetFigures(2, 3)),
                new StatusPaused(40),
                [new StatusCommand(StatusVerb.Cancel, ActionId.From("ApplyCommand:cancel"), ActionClass.Desk, "sindri workflow cancel", Confirm: null)],
                "text form"),
            Facts =
            [
                new FactRow("spec", "Spec", FactType.File, FactDirection.Input, From: null, FactStatus.InProgress, "a file", At: null, By: null,
                    new FactRef("spec.md", 10, "sha256:aa", "p", ItemId: null, Url: null, Host: null, Title: null, FactKey: null), progress,
                    new FactPending(Required: true, ["tests"], ActionId.From("CompleteStep"))),
            ],
            ChildGroups = [new ChildGroup("build", RecipeId.From("workflow.builder-feature"), 4, 9, [new Workstream("ws-1", "UI", [row])], [], More: 6)],
        };
    }

    private static JsonElement Prop(JsonElement parent, string name)
    {
        Assert.True(parent.TryGetProperty(name, out var value), name);
        return value;
    }

    private static void HasExactly(JsonElement element, params string[] names) =>
        Assert.Equal(names.Order(StringComparer.Ordinal), element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

    [Fact]
    public void The_view_serializes_the_section_3_8_names_and_enum_strings()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(Full(), _options));
        var root = doc.RootElement;

        foreach (var name in new[] { "itemId", "urgency", "engineOwned", "subject", "inputs", "orderedBy", "states", "status", "facts", "childGroups" })
        {
            _ = Prop(root, name);
        }

        Assert.Equal("incident", Prop(root, "urgency").GetString());
        Assert.Equal("transition-list", Prop(root, "orderedBy").GetString());
        HasExactly(Prop(root, "engineOwned"), "kind", "reason");
        Assert.Equal("taken-over", Prop(Prop(root, "engineOwned"), "kind").GetString());
        HasExactly(Prop(root, "subject"), "kind", "key", "label", "href");
        Assert.Equal(JsonValueKind.Null, Prop(Prop(root, "subject"), "href").ValueKind);
        HasExactly(Prop(root, "inputs")[0], "key", "label", "kind", "required", "present");

        var cell = Prop(root, "states")[0];
        HasExactly(cell, "id", "order", "branchOf", "glyph", "badge", "round", "loops", "desk", "child", "children", "outcome", "terminal");
        Assert.Equal(JsonValueKind.Null, Prop(cell, "order").ValueKind);
        Assert.Equal("pending", Prop(cell, "glyph").GetString());
        HasExactly(Prop(cell, "round"), "n", "max");
        HasExactly(Prop(cell, "loops")[0], "with", "n", "max");
        HasExactly(Prop(cell, "children"), "done", "total");

        AssertStatus(Prop(root, "status"));
        AssertFactsAndChildren(root);
    }

    private static void AssertStatus(JsonElement status)
    {
        HasExactly(status, "header", "next", "answer", "child", "drift", "inputs", "timeout", "budget", "paused", "commands", "text");
        HasExactly(Prop(status, "header"), "address", "recipe", "hash12", "version", "state", "detail", "since", "ageSeconds");
        HasExactly(Prop(status, "answer"), "kind", "actionId", "class", "cli", "atTerminal");
        Assert.Equal("grantTokens", Prop(Prop(status, "answer"), "kind").GetString());
        Assert.Equal("protected", Prop(Prop(status, "answer"), "class").GetString());
        HasExactly(Prop(status, "child"), "id", "address", "state", "reason");
        HasExactly(Prop(status, "drift"), "block", "needsReapproval");
        HasExactly(Prop(status, "inputs")[0], "key", "name", "hash");
        HasExactly(Prop(status, "timeout"), "kind", "inSeconds", "on");
        Assert.Equal("parks", Prop(Prop(status, "timeout"), "kind").GetString());
        HasExactly(Prop(status, "budget"), "scope", "used", "limit", "instance");
        HasExactly(Prop(Prop(status, "budget"), "instance"), "used", "limit");
        HasExactly(Prop(status, "paused"), "timeoutLeftSeconds");
        HasExactly(Prop(status, "commands")[0], "verb", "actionId", "class", "cli", "confirm");
    }

    private static void AssertFactsAndChildren(JsonElement root)
    {
        var fact = Prop(root, "facts")[0];
        HasExactly(fact, "key", "label", "type", "direction", "from", "status", "summary", "at", "by", "ref", "progress", "pending");
        Assert.Equal("in-progress", Prop(fact, "status").GetString());
        Assert.Equal("input", Prop(fact, "direction").GetString());
        HasExactly(Prop(fact, "ref"), "name", "size", "hash", "path", "itemId", "url", "host", "title", "factKey");
        HasExactly(Prop(fact, "pending"), "required", "checks", "completeAction");

        var group = Prop(root, "childGroups")[0];
        HasExactly(group, "state", "recipe", "done", "total", "workstreams", "rows", "more");
        HasExactly(Prop(group, "workstreams")[0], "id", "label", "rows");
        HasExactly(Prop(Prop(group, "workstreams")[0], "rows")[0], "id", "address", "itemId", "title", "glyph", "state", "chip", "round", "agent", "ageSeconds", "engineOwned", "mirroredPark", "actions");
    }

    [Fact]
    public void The_view_of_an_instance_that_has_not_started_serializes_empty_lists_work_urgency_and_no_status()
    {
        var starting = new InstanceView(
            InstanceAddress.From("T-9/step/1"), StatusCases.View("done").Recipe, "starting", Detail: null, StatusCases.Now, string.Empty, [],
            Question: null, Task: null, Run: null, Children: [], Outcome: null)
        {
            Id = StatusCases.View("done").Id,
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(starting, _options));
        var root = doc.RootElement;

        Assert.Equal("work", Prop(root, "urgency").GetString());
        Assert.Equal("path", Prop(root, "orderedBy").GetString());
        Assert.Equal(JsonValueKind.Null, Prop(root, "status").ValueKind);
        Assert.Equal(0, Prop(root, "states").GetArrayLength());
        Assert.Equal(0, Prop(root, "childGroups").GetArrayLength());
    }

    [Fact]
    public void The_view_round_trips()
    {
        var view = Full();

        var back = JsonSerializer.Deserialize<InstanceView>(JsonSerializer.Serialize(view, _options), _options)!;

        Assert.Equal(JsonSerializer.Serialize(view, _options), JsonSerializer.Serialize(back, _options));
        Assert.Equal((ItemKey.From("ITM-1"), RecipeUrgency.Incident, PathOrdering.TransitionList), (back.ItemId, back.Urgency, back.OrderedBy));
        Assert.Equal("verify", back.States[0].Loops[0].With);
    }

    [Fact]
    public void The_summary_cuts_now_at_80_and_maps_recipe_and_actions()
    {
        var long80 = new string('x', 120);
        var view = Full() with { Next = long80 };

        var summary = InstanceSummaries.Of(view);

        Assert.Equal(new string('x', 77) + "...", summary.Now);
        Assert.Equal(80, summary.Now.Length);
        Assert.Equal(view.Address, summary.Address);
        Assert.Equal(new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, view.Recipe.Version, RecipeUrgency.Incident), summary.Recipe);
        Assert.Equal(view.Status!.Commands.Select(c => c.ActionId), summary.Actions);
        Assert.Equal((ItemKey.From("ITM-1"), view.State, view.Since), (summary.ItemId, summary.State, summary.Since));
        Assert.Same(view.States, summary.States);
        Assert.Equal(view.EngineOwned, summary.EngineOwned);
    }

    [Fact]
    public void The_summary_keeps_a_short_now_whole()
    {
        var exactly80 = new string('y', 80);

        Assert.Equal(exactly80, InstanceSummaries.Of(Full() with { Next = exactly80 }).Now);
        Assert.Equal("short", InstanceSummaries.Of(Full() with { Next = "short" }).Now);
    }
}
