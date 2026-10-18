using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.Tests.Start;

/// <summary>The Start form's state over time (U5', review 351): what an answer leaves behind, what a late read or answer may not change, and what Start waits for.</summary>
[Lane(TestLane.Unit)]
public sealed class StartFormStateTests
{
    private const string Approved = "approved";
    private readonly StartFormFixture _fixture = new();

    private static CancellationToken Token => StartFormFixture.Token;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no repo root above the test binaries");
    }

    private static JsonNode ItemFact(StartInstanceRequest sent) => JsonNode.Parse(sent.Facts["fact:item"].Content.Span)!; // why: the content is JSON

    // ---- what an answer leaves behind ----

    [Fact]
    public async Task After_an_existing_answer_a_new_item_resets_the_sent_state_the_outcome_and_the_new_attempt_offer()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);
        Assert.Equal((false, true, true), (form.CanStart, form.HasOutcome, form.CanStartNewAttempt));

        form.ItemId = "T-2";

        Assert.Equal((true, false, false, false), (form.CanStart, form.HasOutcome, form.OffersNewAttempt, form.CanStartNewAttempt));
    }

    [Fact]
    public async Task After_an_existing_answer_another_recipe_resets_the_sent_state_the_outcome_and_the_new_attempt_offer()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2), (StartFormFixture.Triage, Approved, 2));
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);

        await form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Triage), Token);

        Assert.Equal((true, false, false), (form.CanStart, form.HasOutcome, form.CanStartNewAttempt));
    }

    [Fact]
    public async Task A_new_attempt_is_never_sent_for_an_item_that_was_not_shown_as_finished()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);
        form.ItemId = "T-2";

        await form.StartNewAttempt(Token);

        _ = Assert.Single(_fixture.Server.Started);
    }

    // ---- Start waits for the inputs ----

    [Fact]
    public async Task Start_is_off_while_the_chosen_recipes_inputs_are_read_and_a_press_sends_nothing()
    {
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var gate = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var choosing = form.Choose(form.Recipes[0], Token);
        await form.Submit(Token);

        Assert.Equal((false, 0), (form.CanStart, _fixture.Server.Started.Count));
        gate.SetResult();
        await choosing;
        Assert.True(form.CanStart);
    }

    // ---- stale reads and answers ----

    [Fact]
    public async Task A_recipe_list_read_that_answers_after_the_form_was_closed_and_opened_again_is_dropped()
    {
        var form = await _fixture.Form();
        var first = _fixture.Server.RecipesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstOpen = form.Open("A", Token);
        form.Close();
        var second = _fixture.Server.RecipesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondOpen = form.Open("B", Token);

        first.SetResult();
        await firstOpen;

        Assert.Equal((true, "B", 0), (form.IsLoading, form.ItemId, form.Recipes.Count));
        second.SetResult();
        await secondOpen;
        Assert.Equal((false, true), (form.IsLoading, form.Recipes.Count > 0));
    }

    [Fact]
    public async Task A_recipe_read_that_answers_after_another_recipe_was_chosen_is_dropped()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2), (StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var late = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingStep = form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Step), Token);
        var current = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingTriage = form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Triage), Token);

        late.SetResult();
        await choosingStep;

        Assert.Equal((StartFormFixture.Triage, 0, false), (form.Recipe?.Id, form.Fields.Count, form.CanStart));
        current.SetResult();
        await choosingTriage;
        Assert.Equal((true, true), (form.Fields.Count > 0, form.CanStart));
    }

    [Fact]
    public async Task Closing_the_form_while_a_start_is_in_flight_opens_no_frame_when_the_answer_arrives()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        var opened = 0;
        form.Started += (_, _) => opened++;
        var gate = _fixture.Server.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = form.Submit(Token);

        form.Close();
        gate.SetResult();
        await sending;

        Assert.Equal((0, false, false), (opened, form.IsOpen, form.OpenedFrame));
    }

    // ---- recovery ----

    [Fact]
    public async Task A_recipe_that_could_not_be_read_can_be_chosen_again_and_then_starts()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var good = _fixture.Server.WorkflowAnswer;
        _fixture.Server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");
        await form.Choose(form.Recipes[0], Token);
        Assert.Equal((false, true), (form.CanStart, form.HasNote));

        _fixture.Server.WorkflowAnswer = good;
        await form.Choose(form.Recipes[0], Token);

        Assert.Equal((true, false, true), (form.CanStart, form.HasNote, form.Fields.Count > 0));
    }

    [Fact]
    public async Task A_recipe_chosen_while_the_api_was_down_can_be_chosen_again_when_it_is_back()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var api = (Uri?)StartFormFixture.Api;
        var catalog = new ViewModels.Controls.CatalogViewModel(new FakeClipboard(), _fixture.Clock);
        catalog.Show(StartFormFixture.Api, _fixture.Server, (await _fixture.Server.Commands(StartFormFixture.Api, Token)).Value);
        var form = new StartFormViewModel(catalog, () => api, _fixture.Server, _fixture.Files);
        await form.Open("T-1", Token);
        api = null;
        await form.Choose(form.Recipes[0], Token);
        Assert.False(form.CanStart);

        api = StartFormFixture.Api;
        await form.Choose(form.Recipes[0], Token);

        Assert.Equal((true, true), (form.CanStart, form.Fields.Count > 0));
    }

    // ---- the item fact (review 351 correctness-reviewer-1) ----

    [Fact]
    public async Task A_picked_item_with_a_repo_is_sent_as_the_item_fact_with_its_id_and_repo()
    {
        _fixture.Rows((StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Opened();
        form.Fields[0].Value = "the scope";
        var picked = form.Items.First(i => i.Repo is not null);
        form.PickedItem = picked;

        await form.Submit(Token);

        var fact = ItemFact(Assert.Single(_fixture.Server.Started));
        Assert.Equal((picked.Id, picked.Repo), ((string?)fact["id"], (string?)fact["repo"]));
    }

    [Fact]
    public async Task A_typed_item_the_queue_does_not_list_is_sent_as_the_item_fact_with_its_id_alone()
    {
        _fixture.Rows((StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the scope";

        await form.Submit(Token);

        var fact = ItemFact(Assert.Single(_fixture.Server.Started));
        Assert.Equal(("T-1", 1), ((string?)fact["id"], fact.AsObject().Count));
    }

    // workflow.item's claim step reads the repo out of fact:item (effect.claim.ref): a start from the form must give it one.
    [Fact]
    public async Task The_shipped_item_recipe_claims_a_ref_from_its_item_fact_and_the_form_gives_that_fact_a_repo()
    {
        var recipe = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "recipes", "sindri", "workflow.item.yaml"), Token);
        Assert.Contains("effect.claim.ref", recipe, StringComparison.Ordinal);
        Assert.Contains("{ key: fact:item, label: \"Item\", type: item, kind: item }", recipe, StringComparison.Ordinal);

        _fixture.Rows(("workflow.item", Approved, 2));
        var item = await _fixture.Form();
        await item.Open(itemId: null, Token);
        await item.Choose(item.Recipes[0], Token);
        item.PickedItem = item.Items.First(i => i.Repo is not null);
        await item.Submit(Token);

        Assert.False(string.IsNullOrEmpty((string?)ItemFact(Assert.Single(_fixture.Server.Started))["repo"]));
    }

    // ---- fact names ----

    [Fact]
    public async Task A_link_is_sent_as_a_url_file_and_a_payload_as_a_json_file_under_the_key_without_its_prefix()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "link");
        var link = await _fixture.Opened("T-1");
        link.Fields[0].Value = "https://example.test/spec";
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "payload");
        var payload = await _fixture.Opened("T-1");
        payload.Fields[0].Value = "{}";

        await link.Submit(Token);
        await payload.Submit(Token);

        var names = _fixture.Server.Started.Select(s => s.Facts.Values.Single().Name).ToList();
        Assert.Equal([$"{link.Fields[0].Key["fact:".Length..]}.url", $"{payload.Fields[0].Key["fact:".Length..]}.json"], names);
    }
}
