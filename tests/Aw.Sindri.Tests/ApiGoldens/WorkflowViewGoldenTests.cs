using System.Text.Json;
using Aw.Sindri.Testing.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The golden contract of the rev-8 views (API spec section 3.8; UI screens spec E-20, E-22, E-23): each scenario's fixture
/// (<see cref="WorkflowViewFixtures"/>) serialized through the host's own JSON options, pinned at
/// 'Golden/Api/&lt;slug&gt;/&lt;scenario&gt;.json'. The UI lanes build their fake ISindriApi on these files. API P11 U7 changes the
/// body source to the real routes and leaves the files alone; a file that has to change is a contract change the UI lane is told.
/// </summary>
public sealed class WorkflowViewGoldenTests : IAsyncLifetime
{
    private GoldenHost? _host;

    /// <summary>Every (slug, scenario) pair this class pins, in the order of the plan's golden list.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflow-instances", "root"), ("workflow-instances", "recipe-filter"), ("workflow-instances", "empty"), ("workflow-instances", "open"),
        ("workflow-instance", "project-build"), ("workflow-instance", "incident-loop"), ("workflow-instance", "triage-desk"),
        ("workflow-instance", "escalated-gate"), ("workflow-instance", "dev-desk-round-trip"), ("workflow-instance", "dev-triage-three-state"),
        ("workflow-instance", "transition-list"), ("workflow-instance", "done"), ("workflow-instance", "not-found"),
        ("workflow-status", "parked-child"), ("workflow-status", "desk-task"), ("workflow-status", "parked-budget"),
        ("workflow-status", "escalated-gate"), ("workflow-status", "taken-over"), ("workflow-status", "done"),
        ("workflow-status", "parked-drift"), ("workflow-status", "needs-answer-wait"),
        ("workflow-recipe", "workflow-project"), ("workflow-recipe", "incident-response"), ("workflow-recipe", "no-outputs"),
        ("workflow-instance-fact", "scope-map"), ("workflow-instance-fact", "packet"), ("workflow-instance-fact", "payload"),
        ("workflow-instance-fact", "text"), ("workflow-instance-fact", "truncated"), ("workflow-instance-fact", "not-found"),
    ];

    public static TheoryData<string, string> Rows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in All)
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    public async ValueTask InitializeAsync() => _host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public void View_matches_its_golden_file(string slug, string scenario)
    {
        var host = _host ?? throw new InvalidOperationException("the host isn't started");
        var options = host.App.Services.GetRequiredService<JsonSerializerOptions>();

        ApiGolden.Match(Body(slug, scenario, options), host.Home, slug, scenario);
    }

    private static string Body(string slug, string scenario, JsonSerializerOptions options) => (slug, scenario) switch
    {
        ("workflow-instances", "root") => Json(WorkflowViewFixtures.RootList(), options),
        ("workflow-instances", "recipe-filter") => Json(WorkflowViewFixtures.RecipeFilter(), options),
        ("workflow-instances", "open") => Json(WorkflowViewFixtures.OpenList(), options),
        ("workflow-instances", "empty") => Json(WorkflowViewFixtures.EmptyList(), options),
        ("workflow-instance", "project-build") => Json(WorkflowViewFixtures.ProjectBuild(), options),
        ("workflow-instance", "incident-loop") => Json(WorkflowViewFixtures.IncidentLoop(), options),
        ("workflow-instance", "triage-desk") => Json(WorkflowViewFixtures.TriageDesk(), options),
        ("workflow-instance", "escalated-gate") => Json(WorkflowViewFixtures.EscalatedGate(), options),
        ("workflow-instance", "dev-desk-round-trip") => Json(WorkflowViewFixtures.DeskRoundTrip(), options),
        ("workflow-instance", "dev-triage-three-state") => Json(WorkflowViewFixtures.TriageThreeState(), options),
        ("workflow-instance", "transition-list") => Json(WorkflowViewFixtures.TransitionList(), options),
        ("workflow-instance", "done") => Json(WorkflowViewFixtures.Done(), options),
        ("workflow-instance", "not-found") => NotFound,
        ("workflow-status", "parked-child") => Json(WorkflowViewFixtures.StatusParkedChild(), options),
        ("workflow-status", "desk-task") => Json(WorkflowViewFixtures.StatusDeskTask(), options),
        ("workflow-status", "parked-budget") => Json(WorkflowViewFixtures.StatusParkedBudget(), options),
        ("workflow-status", "escalated-gate") => Json(WorkflowViewFixtures.StatusEscalatedGate(), options),
        ("workflow-status", "taken-over") => Json(WorkflowViewFixtures.StatusTakenOver(), options),
        ("workflow-status", "done") => Json(WorkflowViewFixtures.StatusDone(), options),
        ("workflow-status", "parked-drift") => Json(WorkflowViewFixtures.StatusParkedDrift(), options),
        ("workflow-status", "needs-answer-wait") => Json(WorkflowViewFixtures.StatusNeedsAnswerWait(), options),
        ("workflow-recipe", "workflow-project") => Json(WorkflowViewFixtures.ProjectRecipe(), options),
        ("workflow-recipe", "incident-response") => Json(WorkflowViewFixtures.IncidentRecipe(), options),
        ("workflow-recipe", "no-outputs") => Json(WorkflowViewFixtures.NoOutputsRecipe(), options),
        ("workflow-instance-fact", "not-found") => FactNotFound,
        ("workflow-instance-fact", _) => Json(WorkflowViewFixtures.Fact(scenario), options),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such workflow view scenario"),
    };

    // Pinned by hand until API P11 U7 serves the real bodies: the ProblemDetails of an address in another repo or an unknown
    // one, as the host writes it (ApiProblems). SND-ITEM-404 is not a catalogue code yet; U7 swaps in the served body.
    private const string NotFound =
        "{\n  \"type\": \"https://tools.ietf.org/html/rfc9110#section-15.5.5\",\n  \"title\": \"SND-ITEM-404\",\n  \"status\": 404,\n"
        + "  \"detail\": \"No workflow instance workflow/PRJ-unknown in this repo.\",\n  \"traceId\": \"<trace>\"\n}";

    // The same for a key the recipe doesn't declare (plan row 29): the facts route answers the item's 404 until U7 gives it a code.
    private const string FactNotFound =
        "{\n  \"type\": \"https://tools.ietf.org/html/rfc9110#section-15.5.5\",\n  \"title\": \"SND-ITEM-404\",\n  \"status\": 404,\n"
        + "  \"detail\": \"workflow/PRJ-sync declares no fact 'unknown'.\",\n  \"traceId\": \"<trace>\"\n}";

    private static string Json<T>(T value, JsonSerializerOptions options) =>
        ApiGolden.Reindent(JsonSerializer.Serialize(value, options));
}
