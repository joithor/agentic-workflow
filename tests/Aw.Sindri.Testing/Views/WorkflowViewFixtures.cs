#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The wire shapes of API spec section 3.8 (E-20, E-22, E-23) filled from the screens fixture (2026-10-12, now 12:26)
/// and the two dev recipes' planned state lists. The golden files are these values serialized; the UI lanes build
/// against them. Children are partial until engine P7 (no agent runtime rows, workstreams only on PRJ-sync). Every instance
/// has its own status: its address, recipe stamp, commands and cancel sentence come from its summary row, and the status text
/// is rendered from the status fields (<see cref="StatusText"/>), so none of them can name another instance or disagree.
/// </summary>
public static class WorkflowViewFixtures
{
    /// <summary>The fixture's now.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 26, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset Today(int hour, int minute) => At(10, 12, hour, minute);

    private static string Hash(string prefix) => prefix.PadRight(64, '0');

    private static ActionId Act(string id) => ActionId.From(id);

    private static List<ActionId> Acts(params string[] ids) => [.. ids.Select(Act)];

    private static InstanceRecipe Recipe(string id, string hashPrefix, int version, RecipeUrgency urgency) =>
        new(RecipeId.From(id), RecipeHash.From("sha256:" + Hash(hashPrefix)), version, urgency);

    private static readonly InstanceRecipe _incident = Recipe("workflow.incident-response", "5d90e1b2", 4, RecipeUrgency.Incident);
    private static readonly InstanceRecipe _triage = Recipe("workflow.triage-alert", "d41f2a07", 2, RecipeUrgency.Triage);
    private static readonly InstanceRecipe _project = Recipe("workflow.project", "a8c21e7f", 12, RecipeUrgency.Work);
    private static readonly InstanceRecipe _intake = Recipe("workflow.intake-linear", "0b7e44d3", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _devTriage = Recipe("workflow.dev-triage-three-state", "e2d7a5b1", 1, RecipeUrgency.Triage);
    private static readonly InstanceRecipe _devDesk = Recipe("workflow.dev-desk-round-trip", "93b5c0f4", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _fork = Recipe("workflow.fork-demo", "1f08c6a9", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _step = Recipe("workflow.step", "9ab2d40c", 9, RecipeUrgency.Work);
    private static readonly InstanceRecipe _devTimeout = Recipe("workflow.dev-timeout", "77aa01c3", 3, RecipeUrgency.Work);

    private static readonly string[] _incidentStates = ["assess", "mitigate", "verify", "comms", "resolved"];
    private static readonly string[] _triageStates = ["ingest", "classify", "route", "done"];
    private static readonly string[] _projectStates = ["intake", "scoping", "design", "build", "verify", "done"];
    private static readonly string[] _intakeStates = ["poll", "emit"];
    private static readonly string[] _devTriageStates = ["intake", "classify", "done"];
    private static readonly string[] _devDeskStates = ["intake", "desk", "done"];
    private static readonly string[] _forkStates = ["start", "left", "right", "merge", "done"];

    /// <summary>The state cells of a recipe, path order, with <paramref name="current"/> active and earlier states done.</summary>
    private static List<StateCell> Cells(string[] states, string current, Func<StateCell, StateCell>? shape = null)
    {
        var at = Array.IndexOf(states, current);
        var cells = new List<StateCell>();
        for (var i = 0; i < states.Length; i++)
        {
            var terminal = i == states.Length - 1;
            var glyph = i < at || (terminal && at == i) ? StateGlyph.Done : i == at ? StateGlyph.Active : StateGlyph.Pending;
            var cell = new StateCell(states[i], i + 1, null, glyph, null, null, [], false, false, null, null, terminal);
            cells.Add(shape is null ? cell : shape(cell));
        }

        // WF section 3.10: main-path states are numbered 1, 2, 3 in walk order; a branch state has no order.
        var next = 0;
        return [.. cells.Select(c => c with { Order = c.BranchOf is null ? ++next : null })];
    }

    private static Func<StateCell, StateCell> Shape(params (string State, Func<StateCell, StateCell> Change)[] changes) =>
        cell => changes.Where(c => c.State == cell.State).Aggregate(cell, (acc, c) => c.Change(acc));

    private static SubjectRef Subject(string kind, string key, string label, string? href = null) => new(kind, key, label, href);

    private static InputPresence Input(string key, string label, string kind, bool present = true) => new(key, label, kind, true, present);

    // ---- the instance list (E-20) ------------------------------------------------------------------------------------

    private static InstanceSummary Row(string address, string? item, InstanceRecipe recipe, string state, DateTimeOffset since,
        EngineOwnedBadge? owned, SubjectRef subject, InputPresence[] inputs, List<StateCell> states, string now, params string[] actions) =>
        new(address, IdOf(address), item is null ? null : ItemKey.From(item), recipe, state,
            StateChip.For(state, owned, states.Exists(c => c.Desk && c.Glyph == StateGlyph.Active), states.Exists(c => c.Terminal && c.Glyph == StateGlyph.Done && c.State == state)),
            since, owned, subject, inputs, states, now.Length > 80 ? now[..79] + "…" : now, null, Acts(actions));

    /// <summary>The engine's WFI id of an address (a stable stand-in for the fixture).</summary>
    private static WorkflowInstanceId IdOf(string address) =>
        WorkflowInstanceId.From("WFI-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address)))[..32]);

    private static InstanceSummary DoneTriage(string address, string label, DateTimeOffset since) =>
        Row(address, null, _triage, "done", since, null, Subject("webhook", address, label),
            [Input("alert", "Alert", "webhook")], Cells(_triageStates, "done"), "done");

    private static readonly InstanceSummary _inc14 = Row("workflow/INC-14", null, _incident, "mitigate", Today(11, 2), null,
        Subject("webhook", "P-2211", "PagerDuty P-2211 · Outbox retry storm"), [Input("alert", "Alert", "webhook")],
        Cells(_incidentStates, "mitigate", Shape(
            ("mitigate", c => c with { Round = new Round(1, 3), Loops = [new LoopPair("verify", 1, 3)] }),
            ("verify", c => c with { Loops = [new LoopPair("mitigate", 1, 3)] }),
            ("comms", c => c with { Child = true, Children = new Progress(0, 1) }),
            ("assess", c => c with { Outcome = "sev2" }))),
        "agent rolling back in ITM-01JG2 · comms posting", "OpenItem", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _sentry8812 = Row("workflow/sentry-8812", null, _triage, "classify", Today(12, 22), null,
        Subject("webhook", "8812", "sentry #8812 TypeError in tabs.layout"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "classify", Shape(("classify", c => c with { Desk = true }))),
        "needs your severity (desk answer)", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _p2211 = Row("workflow/pagerduty-P-2211", null, _triage, "route", Today(11, 2),
        new EngineOwnedBadge(EngineOwnedKind.Escalated, "gate.severity 0.91 → INC-14 opened"),
        Subject("webhook", "P-2211", "PagerDuty P-2211"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "route", Shape(("route", c => c with { Badge = new EngineOwnedBadge(EngineOwnedKind.Escalated, "gate.severity 0.91 → INC-14 opened") }))),
        "accept at a terminal", "ApplyCommand:answer-accept", "ApplyCommand:takeover", "ApplyCommand:cancel") with
    { Answer = Answer(AnswerKind.Accept, "workflow/pagerduty-P-2211"), };

    private static readonly InstanceSummary _sentry8809 = Row("workflow/sentry-8809", null, _triage, "done", Today(11, 58), null,
        Subject("webhook", "8809", "sentry #8809 429s on stream"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "done"), "done 12m → ITM-01JF0 queued");

    private static readonly InstanceSummary _prjSync = Row("workflow/PRJ-sync", null, _project, "build", Today(9, 14), null,
        Subject("linear-project", "APP", "APP · Offline sync", "https://linear.app/example/project/offline-sync"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "build", Shape(("build", c => c with { Child = true, Children = new Progress(4, 9) }))),
        "ITM-01JD6 checks · 4 done · 1 running · 1 parked · 3 queued", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjExports = Row("workflow/PRJ-exports", null, _project, "design", At(10, 11, 16, 2),
        new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 10-11 16:02"),
        Subject("linear-project", "ENG", "ENG · Billing exports"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "design", Shape(("design", c => c with { Desk = true, Badge = new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 10-11 16:02") }))),
        "with you: import the design output", "ApplyCommand:answer-resume", "ApplyCommand:handback", "ApplyCommand:cancel") with
    { Answer = Answer(AnswerKind.Resume, "workflow/PRJ-exports", "--resume --text <answer>"), };

    private static readonly InstanceSummary _prjTabs = Row("workflow/PRJ-tabs", null, _project, "scoping", Today(11, 40), null,
        Subject("linear-project", "ENG", "ENG · Tab manager v2"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "scoping", Shape(("scoping", c => c with { Round = new Round(2, 3), Loops = [new LoopPair("scoping", 2, 3)] }))),
        "challenger round 2 · 84k / 600k", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // Instances only the status layouts name: none of them is a row of the root list, so the layouts don't contradict it.
    private static readonly InstanceSummary _prjSearch = Row("workflow/PRJ-search", null, _project, "build", Today(11, 54), null,
        Subject("linear-project", "SRCH", "SRCH · Search"), [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "build", Shape(("build", c => c with { Child = true, Children = new Progress(2, 5) }))),
        "parked child", "ApplyCommand:answer-grant", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _budgetStep = Row("workflow/ITM-01JC8/step", "ITM-01JC8", _step, "produce", Today(11, 54), null,
        Subject("item", "ITM-01JC8", "ITM-01JC8"), [Input("item", "Item", "item")],
        Cells(["produce", "checks", "done"], "produce"), "parked: budget", "ApplyCommand:answer-grant-tokens", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _takenOver = Row("workflow/ITM-01JA1", "ITM-01JA1", _devTimeout, "taken-over", Today(12, 1), null,
        Subject("item", "ITM-01JA1", "ITM-01JA1"), [Input("item", "Item", "item")],
        Cells(["start", "wait", "done"], "wait"), "with you", "ApplyCommand:handback", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjCache = Row("workflow/PRJ-cache", null, _project, "scoping", Today(11, 50), null,
        Subject("linear-project", "ENG", "ENG · Cache layer"), [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "scoping"), "parked: spec-conflict", "ApplyCommand:answer-resume", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static InstanceSummary Intake(string team, DateTimeOffset since, string now) =>
        Row($"workflow/LIN-{team}", null, _intake, "poll", since, null, Subject("linear-team", team, $"Linear {team}"),
            [Input("team", "Linear team", "linear-team")], Cells(_intakeStates, "poll"), now, "ApplyCommand:cancel");

    private static IReadOnlyList<InstanceSummary> Roots() =>
    [
        _inc14, _sentry8812, _prjSync, _prjExports, _prjTabs, _p2211, _sentry8809,
        DoneTriage("workflow/sentry-8801", "sentry #8801", Today(10, 30)),
        DoneTriage("workflow/sentry-8797", "sentry #8797", Today(10, 5)),
        DoneTriage("workflow/sentry-8790", "sentry #8790", Today(9, 40)),
        Intake("ENG", Today(12, 10), "polling every 15m · last 12:15 · 2 new items"),
        Intake("APP", Today(12, 15), "polling every 15m · last 12:15 · 2 new items"),
    ];

    private static ListCounts CountsOf(IReadOnlyList<InstanceSummary> roots) => new(
        roots.Count(r => r.Chip.Word != "DONE"), roots.Count(r => r.Chip.Tone == ChipTone.Attn),
        roots.Count(r => r.Chip.Word == "WITH YOU"), roots.Count(r => r.Chip.Word == "RUNNING"), roots.Count(r => r.Chip.Word == "DONE"));

    // The counts are over the whole root set, before the recipe filter or 'open' narrows the rows (API spec section 3.8).
    private static InstanceList Ordered(IEnumerable<InstanceSummary> rows) =>
        new([.. rows.OrderBy(r => r.Recipe.Urgency).ThenBy(r => r.Since)], CountsOf(Roots()), null, Now);

    /// <summary>The Workflows page: the 12 roots in urgency order, then oldest since.</summary>
    public static InstanceList RootList() => Ordered(Roots());

    /// <summary>The roots of recipe workflow.triage-alert.</summary>
    public static InstanceList RecipeFilter() => Ordered(Roots().Where(r => r.Recipe.Id.Value == "workflow.triage-alert"));

    /// <summary>The open roots ('?root=true&amp;open=true'): the rows not finished, counts unchanged.</summary>
    public static InstanceList OpenList() => Ordered(Roots().Where(r => r.Chip.Word != "DONE"));

    /// <summary>No instances.</summary>
    public static InstanceList EmptyList() => new([], new ListCounts(0, 0, 0, 0, 0), InstanceList.EmptyText, Now);

    // ---- the status fields (E-23, WF section 6.4) --------------------------------------------------------------------

    /// <summary>
    /// The status of one instance from its summary row: the header, the commands (the row's actions that are verbs, in the
    /// catalog's order) and the text, all from the row, so a status cannot name another instance.
    /// </summary>
    private static StatusFields Status(InstanceSummary row, string? detail, string next, OpenWork open, StatusLines? lines = null)
    {
        lines ??= new StatusLines();
        var recipe = row.Recipe;
        var header = new StatusHeader(row.Address, recipe.Id, recipe.Hash.Display, recipe.Version, row.State, detail, row.Since, (long)(Now - row.Since).TotalSeconds);
        var fields = new StatusFields(header, next, lines.Answer, lines.Child, lines.Drift, lines.Inputs, lines.Timeout, lines.Budget, lines.Paused,
            Commands(row, lines.Answer, open), string.Empty);
        return fields with { Text = StatusText.Render(fields, Now) };
    }

    private static List<StatusCommand> Commands(InstanceSummary row, StatusAnswer? answer, OpenWork open)
    {
        var commands = new List<StatusCommand>();
        foreach (var verb in new[] { "answer", "complete", "handback", "takeover", "cancel" })
        {
            var found = row.Actions.Where(a => VerbOf(a.Value) == verb).ToList();
            if (found.Count == 0)
            {
                continue;
            }

            var (actionClass, cli, confirm) = verb switch
            {
                "cancel" => (ActionClass.Desk, $"sindri workflow cancel {row.Address}", ConfirmText.Cancel(row.Address, open.RunningSteps, open.OpenChildren)),
                "answer" => (answer!.Class, answer.Cli, null),
                "complete" => (ActionClass.Desk, open.CompleteCli!, null),
                "takeover" => (ActionClass.Pause, $"sindri workflow takeover {row.Address}", null),
                "handback" => (ActionClass.Desk, $"sindri workflow handback {row.Address}", null),
                _ => throw new InvalidOperationException($"no command line for the verb {verb}"),
            };
            commands.Add(new StatusCommand(CommandVerb.From(verb), found[0], actionClass, cli, confirm));
        }

        return commands;
    }

    private static string? VerbOf(string action) => action switch
    {
        "CompleteStep" => "complete",
        _ when action.StartsWith("ApplyCommand:answer", StringComparison.Ordinal) => "answer",
        _ when action.StartsWith("ApplyCommand:", StringComparison.Ordinal) => action["ApplyCommand:".Length..],
        _ => null,
    };

    /// <summary>The answer a park takes, with the catalog's action id and class for its kind (grant, grantTokens and accept are protected, at a terminal).</summary>
    private static StatusAnswer Answer(AnswerKind kind, string address, string? arguments = null) => kind switch
    {
        AnswerKind.Grant => new(kind, Act("ApplyCommand:answer-grant"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--grant 1"}", AtTerminal: true),
        AnswerKind.GrantTokens => new(kind, Act("ApplyCommand:answer-grant-tokens"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--grant-tokens 100k"}", AtTerminal: true),
        AnswerKind.Accept => new(kind, Act("ApplyCommand:answer-accept"), ActionClass.Protected, $"sindri workflow answer {address} {arguments ?? "--accept"}", AtTerminal: true),
        AnswerKind.Resume => new(kind, Act("ApplyCommand:answer-resume"), ActionClass.Desk, $"sindri workflow answer {address} {arguments ?? "--resume"}", AtTerminal: false),
        AnswerKind.Text => new(kind, Act("ApplyCommand:answer-text"), ActionClass.Desk, $"sindri workflow answer {address} {arguments ?? "--text <answer>"}", AtTerminal: false),
        AnswerKind.CancelOnly => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no answer for this kind"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no answer for this kind"),
    };

    /// <summary>What the cancel sentence and the complete command need from the instance's open work; N and M come from the fixture until engine P4b.</summary>
    /// <param name="RunningSteps">The open run's running steps (N).</param>
    /// <param name="OpenChildren">The open children (M).</param>
    /// <param name="CompleteCli">The complete command, when a desk is open.</param>
    private sealed record OpenWork(int RunningSteps = 0, int OpenChildren = 0, string? CompleteCli = null);

    /// <summary>The layout's optional lines.</summary>
    private sealed record StatusLines
    {
        public StatusAnswer? Answer { get; init; }

        public StatusChild? Child { get; init; }

        public StatusDrift? Drift { get; init; }

        public IReadOnlyList<StatusInput> Inputs { get; init; } = [];

        public StatusTimeout? Timeout { get; init; }

        public StatusBudget? Budget { get; init; }

        public StatusPaused? Paused { get; init; }
    }

    private static readonly StatusInput _alertInput = new("fact:alert", "alert.json", "1c0e5a77");

    private static StatusFields DeskStatus(InstanceSummary row, string produce, string run, string outputKey, string outputSample, StatusInput input)
    {
        var complete = $"sindri workflow complete {row.Address} --run {run} --outcome done --output fact:{outputKey}={outputSample}";
        return Status(row, "desk task", $"{produce}, then run: {complete}", new OpenWork(CompleteCli: complete), new StatusLines { Inputs = [input] });
    }

    /// <summary>Layout 1: a park that mirrors a parked child; the grant is forwarded to it.</summary>
    public static StatusFields StatusParkedChild() => Status(_prjSearch, "parked: no-progress (child)",
        "Parked (no-progress) in workflow/PRJ-search/ITM-01JF9: checks failed twice with the same findings. Grant more rounds at a terminal, or cancel.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = Answer(AnswerKind.Grant, "workflow/PRJ-search"), Child = new StatusChild("workflow/PRJ-search/ITM-01JF9", "parked", "no-progress") });

    /// <summary>Layout 2: an open desk task with its inputs, a parking timeout and the state and instance budgets.</summary>
    public static StatusFields StatusDeskTask() => Status(_sentry8812, "desk task",
        "Produce the Step output for sentry-8812 (round 1/3), then run: sindri workflow complete workflow/sentry-8812 --run 1.1 --outcome done --output fact:severity=<text>",
        new OpenWork(CompleteCli: "sindri workflow complete workflow/sentry-8812 --run 1.1 --outcome done --output fact:severity=<text>"),
        new StatusLines
        {
            Inputs = [_alertInput],
            Timeout = new StatusTimeout(TimeoutKind.Parks, 165_000, "desk"),
            Budget = new StatusBudget("state", 0, 200_000, new BudgetFigures(0, 400_000)),
        });

    /// <summary>Layout 3: a budget park; the answer grants tokens.</summary>
    public static StatusFields StatusParkedBudget() => Status(_budgetStep, "parked: budget", "Parked (budget): produce used 200k of 200k tokens.",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.GrantTokens, "workflow/ITM-01JC8/step") });

    /// <summary>Layout 4: an escalated gate; the answer accepts at a terminal.</summary>
    public static StatusFields StatusEscalatedGate() => Status(_p2211, "escalated: gate",
        "Escalated: gate.severity scored 0.91, above 0.8. Accept to override it (the other checks still run before done), or reject (the Step re-runs within its loop, otherwise this parks).",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.Accept, "workflow/pagerduty-P-2211") });

    /// <summary>Layout 5: a taken-over instance with its timeout paused.</summary>
    public static StatusFields StatusTakenOver() => Status(_takenOver, null,
        "With you since 12:01. Run `sindri workflow handback workflow/ITM-01JA1` when you're done.", new OpenWork(), new StatusLines { Paused = new StatusPaused(40) });

    /// <summary>Layout 6: a finished instance, the header only.</summary>
    public static StatusFields StatusDone() => Status(_sentry8809, "resolved", string.Empty, new OpenWork());

    /// <summary>A park with a recorded drift: the answer needs the re-approval first, which is its own protected command; the resume stays desk.</summary>
    public static StatusFields StatusParkedDrift() => Status(_prjCache, "parked: spec-conflict",
        "Parked (spec-conflict, drift): block step.scope.draft changed in this build. Re-approve it at a terminal, then resume.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Resume, "workflow/PRJ-cache"), Drift = new StatusDrift(BlockId.From("step.scope.draft"), NeedsReapproval: true) });

    /// <summary>A needs-answer wait: the answer is a bare resume with optional text.</summary>
    public static StatusFields StatusNeedsAnswerWait() => Status(_prjExports, "parked: needs-answer",
        "Parked (needs-answer) in design: import the design output, then resume.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Resume, "workflow/PRJ-exports", "--resume --text <answer>") });

    /// <summary>Every status the fixtures serve: the eight layouts, then the status block of each instance view.</summary>
    /// <returns>The statuses.</returns>
    public static IReadOnlyList<StatusFields> AllStatuses() =>
    [
        StatusParkedChild(), StatusDeskTask(), StatusParkedBudget(), StatusEscalatedGate(), StatusTakenOver(), StatusDone(),
        StatusParkedDrift(), StatusNeedsAnswerWait(), .. AllDetails().Select(d => d.Current.Status),
    ];

    /// <summary>Every instance view the fixtures serve.</summary>
    /// <returns>The views.</returns>
    public static IReadOnlyList<InstanceDetail> AllDetails() =>
        [ProjectBuild(), IncidentLoop(), TriageDesk(), EscalatedGate(), DeskRoundTrip(), TriageThreeState(), TransitionList(), Done()];

    // ---- facts and children ------------------------------------------------------------------------------------------

    private static FactRow Fact(string key, string label, FactType type, FactDirection direction, string? from, FactStatus status, string summary,
        DateTimeOffset? at = null, string? by = null, FactRef? reference = null, Progress? progress = null, FactPending? pending = null) =>
        new(key, label, type, direction, from, status, summary, at, by, reference, progress, pending);

    private static FactRef Ref(string? name = null, long? size = null, string? hash = null, string? path = null, string? item = null,
        string? url = null, string? host = null, string? title = null, string? factKey = null) =>
        new(name, size, hash, path, item is null ? null : ItemKey.From(item), url, host, title, factKey);

    private static ChildRow Child(string item, string title, StateGlyph glyph, string state, EngineOwnedBadge? owned = null, EngineOwnedBadge? park = null, Round? round = null, string? agent = null, long age = 600) =>
        new($"workflow/{item}", ItemKey.From(item), title, glyph, state, StateChip.For(state, owned, false, state == "done"), round, agent, age, owned, park, Acts("OpenItem", "ApplyCommand:takeover"));

    private static ViewCounts Counts(int timeline, int decisions, int requests) => new(timeline, decisions, requests, null);

    // ---- the instance views (E-20) -----------------------------------------------------------------------------------

    private static InstanceDetail Detail(InstanceSummary row, string title, PathOrdering ordering, CurrentState current, IReadOnlyList<FactRow> facts,
        IReadOnlyList<ChildGroup> children, ViewCounts counts) =>
        new(row.Address, row.Id, null, row.ItemId, title, row.Recipe, row.State, row.Since, row.EngineOwned, row.Subject, ordering, row.States, current, facts, children, counts, row.Actions, Now);

    /// <summary>PRJ-sync: build, four of nine children done in four workstreams, a scope map, a packet and evidence; one child parked.</summary>
    public static InstanceDetail ProjectBuild()
    {
        var status = Status(_prjSync, "children 4 of 9", "Waiting on 9 children: 4 done · 1 running · 1 parked · 3 queued.", new OpenWork(OpenChildren: 5),
            new StatusLines
            {
                Timeout = new StatusTimeout(TimeoutKind.Escalates, 86_400, "build"),
                Budget = new StatusBudget("state", 1_200_000, 4_000_000, new BudgetFigures(1_900_000, 6_000_000)),
            });
        var ws1 = new Workstream("ws-1", "UI",
            [Child("ITM-01JC3", "Sync status pill", StateGlyph.Done, "done"), Child("ITM-01JC5", "Conflict resolver dialog", StateGlyph.Done, "done")]);
        var ws2 = new Workstream("ws-2", "Store",
            [Child("ITM-01JC1", "PUT /sync/changes", StateGlyph.Done, "done"), Child("ITM-01JC2", "Outbox queue", StateGlyph.Done, "done"), Child("ITM-01JD6", "Services page job panel", StateGlyph.Active, "checks", age: 0)]);
        var park = new EngineOwnedBadge(EngineOwnedKind.Parked, "no-progress");
        var ws3 = new Workstream("ws-3", "Mobile",
            [Child("ITM-01JD7", "Background sync (iOS)", StateGlyph.Active, "produce", owned: park, park: park), Child("ITM-01JF3", "doctor views line", StateGlyph.Pending, "queued"), Child("ITM-01JF4", "Tray badge on Linux", StateGlyph.Pending, "queued")]);
        var ws4 = new Workstream("ws-4", "Rollout", [Child("ITM-01JF8", "Receipt verify route", StateGlyph.Pending, "queued")]);
        return Detail(_prjSync, "Offline sync", PathOrdering.Path,
            new CurrentState("build", new StepDeclaration("step.children", "builder.feature", "one child per packet task"),
                [new GateLine(BlockId.From("gate.evidence.all"), "exit", null, "Every child has its evidence pack."), new GateLine(BlockId.From("gate.judge.verify"), "exit", 0.8, "The judge scores the build at least 0.8.")],
                null, status),
            [
                Fact("project", "Linear project", FactType.Link, FactDirection.Input, null, FactStatus.Produced, "Linear APP · Offline sync", At(10, 12, 9, 14), "human:cli",
                    Ref(url: "https://linear.app/example/project/offline-sync", host: "linear.app", title: "APP · Offline sync")),
                Fact("prd", "PRD", FactType.File, FactDirection.Input, null, FactStatus.Produced, "PRD-sync.md", At(10, 12, 9, 14), "human:cli",
                    Ref(name: "PRD-sync.md", size: 18_432, hash: Hash("1c0e5a77"), path: "docs/PRD-sync.md")),
                Fact("scope", "Scope map", FactType.ScopeMap, FactDirection.Output, "scoping", FactStatus.Produced, "12 areas, 31 cited files", At(10, 12, 9, 40), "step.scope.draft", Ref(factKey: "scope")),
                Fact("packet", "Packet 3f1c09", FactType.Packet, FactDirection.Output, "design", FactStatus.Produced, "9 tasks in 4 workstreams, frozen", At(10, 12, 10, 5), "step.packet.freeze", Ref(factKey: "packet")),
                Fact("evidence", "Evidence", FactType.Evidence, FactDirection.Output, "build", FactStatus.InProgress, "4 of 9 packs", null, null, Ref(factKey: "evidence"), new Progress(4, 9)),
            ],
            [new ChildGroup("build", "builder.feature", 4, 9, [ws1, ws2, ws3, ws4], [], 0)],
            Counts(41, 3, 1));
    }

    /// <summary>INC-14: the mitigate and verify loop at round 1 of 3, a recorded sev2 outcome, a running comms child, a timeout that escalates.</summary>
    public static InstanceDetail IncidentLoop()
    {
        var status = Status(_inc14, "round 1/3", "agent rolling back in ITM-01JG2 · comms posting", new OpenWork(RunningSteps: 1, OpenChildren: 1),
            new StatusLines { Timeout = new StatusTimeout(TimeoutKind.Escalates, 3_600, "mitigate") });
        return Detail(_inc14, "Outbox retry storm", PathOrdering.Path,
            new CurrentState("mitigate", new StepDeclaration("step.agent", "mitigate.rollback", "roll back the last release"), [], null, status),
            [
                Fact("alert", "Alert", FactType.Link, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "human:webhook",
                    Ref(url: "https://pagerduty.example/incidents/P-2211", host: "pagerduty.example", title: "P-2211")),
                Fact("severity", "Severity", FactType.Decision, FactDirection.Output, "assess", FactStatus.Produced, "sev2", At(10, 12, 11, 4), "step.assess"),
                Fact("postmortem", "Postmortem draft", FactType.Text, FactDirection.Output, "resolved", FactStatus.Pending, "not started"),
            ],
            [new ChildGroup("comms", "builder.comms", 0, 1, [], [Child("ITM-01JG3", "Post status updates", StateGlyph.Active, "post", age: 300)], 0)],
            Counts(12, 1, 0));
    }

    /// <summary>sentry #8812: the classify desk is open and asks for a severity.</summary>
    public static InstanceDetail TriageDesk()
    {
        var pending = new FactPending(true, ["output.nonempty"], Act("CompleteStep"));
        return Detail(_sentry8812, "TypeError in tabs.layout", PathOrdering.Path,
            new CurrentState("classify", new StepDeclaration("step.desk", "classify.severity", "a person classifies the alert"),
                [], new DeskForm(RunToken.From("1.1"), [new DeskOutput("severity", FactType.Text, true)], ["output.nonempty"], Act("CompleteStep")), StatusDeskTask()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8812 TypeError", At(10, 12, 12, 22), "human:webhook", Ref(factKey: "alert")),
                Fact("severity", "Severity", FactType.Text, FactDirection.Output, "classify", FactStatus.Pending, "awaiting your answer", pending: pending),
            ],
            [], Counts(3, 0, 1));
    }

    /// <summary>pagerduty P-2211: escalated at the severity gate; the accept needs a terminal.</summary>
    public static InstanceDetail EscalatedGate() =>
        Detail(_p2211, "PagerDuty P-2211", PathOrdering.Path,
            new CurrentState("route", new StepDeclaration("step.tool", "route.severity", "route by severity"),
                [new GateLine(BlockId.From("gate.severity"), "exit", 0.8, "Severity under 0.8 routes to a ticket; above it opens an incident.")], null, StatusEscalatedGate()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "human:webhook", Ref(factKey: "alert")),
                Fact("route", "Route", FactType.Decision, FactDirection.Output, "route", FactStatus.InProgress, "gate.severity 0.91 → INC-14 opened"),
            ],
            [], Counts(6, 1, 0));

    private static InstanceSummary DevRow(string name, InstanceRecipe recipe, string[] states, string state, Func<StateCell, StateCell>? shape, params string[] actions) =>
        Row($"workflow/{name}", null, recipe, state, Today(12, 0), null, Subject("text", name, name), [Input("request", "Request", "text")], Cells(states, state, shape), "next step", actions);

    /// <summary>The desk-round-trip dev recipe, desk open (the P11 signal fixture).</summary>
    public static InstanceDetail DeskRoundTrip()
    {
        var row = DevRow("desk-round-trip-1", _devDesk, _devDeskStates, "desk", Shape(("desk", c => c with { Desk = true })), "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Desk round trip", PathOrdering.Path,
            new CurrentState("desk", new StepDeclaration("step.desk", "desk.produce", "a person produces the file"),
                [], new DeskForm(RunToken.From("1.1"), [new DeskOutput("report", FactType.File, true)], ["output.file"], Act("CompleteStep")),
                DeskStatus(row, "Produce the report file", "1.1", "report", "<file>", new StatusInput("fact:request", "request.txt", "7ab2c013"))),
            [
                Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Summarize the week", At(10, 12, 12, 0), "human:cli"),
                Fact("report", "Report", FactType.File, FactDirection.Output, "desk", FactStatus.Pending, "awaiting your file",
                    pending: new FactPending(true, ["output.file"], Act("CompleteStep"))),
            ],
            [], Counts(2, 0, 0));
    }

    /// <summary>The three-state triage dev recipe: 'order' follows the transition order.</summary>
    public static InstanceDetail TriageThreeState()
    {
        var row = DevRow("triage-three-state-1", _devTriage, _devTriageStates, "classify", null, "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Three-state triage", PathOrdering.Path,
            new CurrentState("classify", new StepDeclaration("step.tool", "classify.rules", "classify by rules"), [], null,
                Status(row, "running", "classify.rules is running.", new OpenWork(RunningSteps: 1), new StatusLines { Inputs = [new StatusInput("fact:request", "request.txt", "5cd0e9a2")] })),
            [
                Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Crash on launch", At(10, 12, 12, 0), "human:cli"),
                Fact("label", "Label", FactType.Text, FactDirection.Output, "classify", FactStatus.InProgress, "classifying"),
            ],
            [], Counts(2, 0, 0));
    }

    /// <summary>A recipe with two initial paths: the band says transition-list and folds 'right' under 'left' (a branch has no order).</summary>
    public static InstanceDetail TransitionList()
    {
        var row = Row("workflow/fork-demo-1", null, _fork, "right", Today(12, 0), null, Subject("text", "fork-demo", "Fork demo"),
            [Input("request", "Request", "text")],
            Cells(_forkStates, "right", Shape(("right", c => c with { BranchOf = "left" }))), "next step", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Fork demo", PathOrdering.TransitionList,
            new CurrentState("right", new StepDeclaration("step.tool", "branch.right", "the right branch"), [], null,
                Status(row, "running", "branch.right is running.", new OpenWork(RunningSteps: 1))),
            [Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Fork it", At(10, 12, 12, 0), "human:cli")],
            [], Counts(2, 0, 0));
    }

    /// <summary>sentry #8809, finished: a header-only status and no commands.</summary>
    public static InstanceDetail Done() =>
        Detail(_sentry8809, "429s on stream", PathOrdering.Path,
            new CurrentState("done", null, [], null, StatusDone()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8809 429s on stream", At(10, 12, 11, 40), "human:webhook", Ref(factKey: "alert")),
                Fact("item", "Item", FactType.Item, FactDirection.Output, "route", FactStatus.Produced, "ITM-01JF0 queued", At(10, 12, 11, 58), "step.route", Ref(item: "ITM-01JF0")),
            ],
            [], Counts(7, 0, 0));

    // ---- the recipe additions (E-22) ---------------------------------------------------------------------------------

    private static RecipeStateView[] StateViews(string[] states) => [.. states.Select((s, i) => new RecipeStateView(s, i + 1, null))];

    /// <summary>workflow.project: a Linear project input, outputs with their producing states, urgency work.</summary>
    public static RecipeView ProjectRecipe() => new(
        [new RecipeInputView("project", "Linear project", InputType.Text, "linear-project", true), new RecipeInputView("prd", "PRD", InputType.File, null, true)],
        [new RecipeOutputView("scope", "Scope map", FactType.ScopeMap, "scoping"), new RecipeOutputView("packet", "Packet", FactType.Packet, "design"), new RecipeOutputView("evidence", "Evidence", FactType.Evidence, "build")],
        RecipeUrgency.Work, StateViews(_projectStates), []);

    /// <summary>incident.response: urgency incident and the mitigate and verify loop.</summary>
    public static RecipeView IncidentRecipe() => new(
        [new RecipeInputView("alert", "Alert", InputType.Text, "webhook", true)],
        [new RecipeOutputView("severity", "Severity", FactType.Decision, "assess"), new RecipeOutputView("postmortem", "Postmortem draft", FactType.Text, "resolved")],
        RecipeUrgency.Incident, StateViews(_incidentStates), [new RecipeLoopView("remediate", "mitigate", "verify")]);

    /// <summary>A recipe with no outputs: 'outputs: []', and a kind on the first input only.</summary>
    public static RecipeView NoOutputsRecipe() => new(
        [new RecipeInputView("request", "Request", InputType.Text, "text", true), new RecipeInputView("context", null, InputType.File, null, false)],
        [], RecipeUrgency.Work, StateViews(_devTriageStates), []);

    // ---- the facts route (GetInstanceFact) ---------------------------------------------------------------------------

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, text, payload or truncated (see <see cref="FactFixtures"/>).</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => FactFixtures.Fact(scenario);
}
