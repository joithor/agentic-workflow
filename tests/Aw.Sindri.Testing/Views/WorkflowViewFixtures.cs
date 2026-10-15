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
    private static readonly InstanceRecipe _devTriage = Recipe("workflow.dev-triage-three-state", "88a0e8fc810fb8dbdccb244b1a601e16743ade3c4230b57c7c0cf782b3ed0f66", 1, RecipeUrgency.Triage);
    private static readonly InstanceRecipe _devDesk = Recipe("workflow.dev-desk-round-trip", "42e9e8ab897f90e38c74f9a3a5aa499f9a56475b4c1a416c0ad5b01a79bb2346", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _fork = Recipe("workflow.fork-demo", "1f08c6a9", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _item = Recipe("workflow.item", "496cdcbfe884c70dce832d96d21883660f29fffe9b122fcfa72a04c98be8cae3", 1, RecipeUrgency.Work);

    private static readonly InstanceRecipe _step = Recipe("workflow.step", "1b13bd920351e56227141fe24e59f2f90cb46bc3549e1e90f2da2af1c3be9f5c", 9, RecipeUrgency.Work);

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

    /// <summary>The project cells: the scoping loop belongs to the recipe hash, so every instance carries it (an empty round where it isn't looping).</summary>
    private static List<StateCell> ProjectCells(string current, Func<StateCell, StateCell>? shape = null) =>
        Cells(_projectStates, current, cell => shape is null ? WithScopingLoop(cell) : shape(WithScopingLoop(cell)));

    private static StateCell WithScopingLoop(StateCell cell) => cell.State == "scoping" ? cell with { Loops = [new LoopPair("scoping", 0, 3)] } : cell;

    private static Func<StateCell, StateCell> Shape(params (string State, Func<StateCell, StateCell> Change)[] changes) =>
        cell => changes.Where(c => c.State == cell.State).Aggregate(cell, (acc, c) => c.Change(acc));

    private static SubjectRef Subject(string kind, string key, string label, string? href = null) => new(kind, key, label, href);

    private static InputPresence Input(string key, string label, string? kind, bool present = true) => new(key, label, kind, true, present);

    // ---- the instance list (E-20) ------------------------------------------------------------------------------------

    private static InstanceSummary Row(string address, InstanceRecipe recipe, string state, DateTimeOffset since,
        EngineOwnedBadge? owned, SubjectRef subject, InputPresence[] inputs, List<StateCell> cells, string now, params string[] actions)
    {
        var states = cells.ConvertAll(c => owned is not null && c.State == state && c.Badge is null ? c with { Badge = owned } : c);
        return new(InstanceAddress.From(address), IdOf(address), ItemKey.From(address[..address.IndexOf('/', StringComparison.Ordinal)]), recipe, state,
            StateChip.For(state, owned, states.Exists(c => c.Desk && c.Glyph == StateGlyph.Active), states.Exists(c => c.Terminal && c.Glyph == StateGlyph.Done && c.State == state)),
            since, owned, subject, inputs, states, now.Length > 80 ? now[..79] + "…" : now, null, Acts(actions));
    }

    /// <summary>
    /// The engine's id of an address: a copy of WorkflowInstanceId.Of from engine #192 (feat/sindri-wf-p4-interpreter), a UUIDv8
    /// over the SHA-256 of the address, so the goldens carry the ids the engine mints. Delete this once #192 is on main.
    /// </summary>
    public static WorkflowInstanceId IdOf(string address)
    {
        Span<byte> digest = stackalloc byte[System.Security.Cryptography.SHA256.HashSizeInBytes];
        _ = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address), digest);
        var bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return WorkflowInstanceId.For(new Guid(bytes, bigEndian: true));
    }

    private static InstanceSummary DoneTriage(string address, string label, DateTimeOffset since) =>
        Row(address, _triage, "done", since, null, Subject("webhook", label[(label.IndexOf('#', StringComparison.Ordinal) + 1)..], label),
            [Input("alert", "Alert", "webhook")], Cells(_triageStates, "done"), "done");

    private static readonly InstanceSummary _inc14 = Row("ITM-01JK1/incident-response/1", _incident, "mitigate", Today(11, 2), null,
        Subject("webhook", "P-2211", "PagerDuty P-2211 · Outbox retry storm"), [Input("alert", "Alert", "webhook")],
        Cells(_incidentStates, "mitigate", Shape(
            ("mitigate", c => c with { Round = new Round(1, 3), Loops = [new LoopPair("verify", 1, 3)] }),
            ("verify", c => c with { Loops = [new LoopPair("mitigate", 1, 3)] }),
            ("comms", c => c with { Glyph = StateGlyph.Active, Child = true, Children = new Progress(0, 1) }),
            ("assess", c => c with { Outcome = "sev2" }))),
        "agent rolling back in ITM-01JG2 · comms posting", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _sentry8812 = Row("ITM-01JK2/triage-alert/1", _triage, "classify", Today(12, 22), null,
        Subject("webhook", "8812", "sentry #8812 TypeError in tabs.layout"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "classify", Shape(("classify", c => c with { Desk = true }))),
        "needs your severity (desk answer)", "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _p2211 = Row("ITM-01JK6/triage-alert/1", _triage, "route", Today(11, 2),
        new EngineOwnedBadge(EngineOwnedKind.Escalated, "gate.severity 0.91 → INC-14 opened"),
        Subject("webhook", "P-2211", "PagerDuty P-2211"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "route", Shape(("route", c => c with { Badge = new EngineOwnedBadge(EngineOwnedKind.Escalated, "gate.severity 0.91 → INC-14 opened") }))),
        "accept at a terminal", "ApplyCommand:answer-accept", "ApplyCommand:takeover", "ApplyCommand:cancel") with
    { Answer = Answer(AnswerKind.Accept, "ITM-01JK6/triage-alert/1"), };

    private static readonly InstanceSummary _sentry8809 = Row("ITM-01JK7/triage-alert/1", _triage, "done", Today(11, 58), null,
        Subject("webhook", "8809", "sentry #8809 429s on stream"), [Input("alert", "Alert", "webhook")],
        Cells(_triageStates, "done"), "done 12m → ITM-01JF0 queued");

    private static readonly InstanceSummary _prjSync = Row("ITM-01JK3/project/1", _project, "build", Today(9, 14), null,
        Subject("linear-project", "APP", "APP · Offline sync", "https://linear.app/example/project/offline-sync"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("build", Shape(("build", c => c with { Child = true, Children = new Progress(4, 9) }))),
        "ITM-01JD6 checks · 4 done · 1 running · 1 parked · 3 queued", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjExports = Row("ITM-01JK4/project/1", _project, "design", At(10, 11, 16, 2),
        new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 10-11 16:02"),
        Subject("linear-project", "ENG", "ENG · Billing exports"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("design", Shape(("design", c => c with { Desk = true }))),
        "with you: import the design output", "ApplyCommand:handback", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjTabs = Row("ITM-01JK5/project/1", _project, "scoping", Today(11, 40), null,
        Subject("linear-project", "ENG", "ENG · Tab manager v2"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("scoping", Shape(("scoping", c => c with { Round = new Round(2, 3), Loops = [new LoopPair("scoping", 2, 3)] }))),
        "challenger round 2 · 84k / 600k", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // Instances only the status layouts name: none of them is a row of the root list, so the layouts don't contradict it.
    private static readonly InstanceSummary _prjSearch = Row("ITM-01JKD/project/1", _project, "build", Today(11, 54), new EngineOwnedBadge(EngineOwnedKind.Parked, "no-progress (child)"),
        Subject("linear-project", "SRCH", "SRCH · Search"), [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("build", Shape(("build", c => c with { Child = true, Children = new Progress(2, 5) }))),
        "parked child", "ApplyCommand:answer-grant", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _budgetStep = Row("ITM-01JH5/step/1", _step, "produce", Today(11, 54), new EngineOwnedBadge(EngineOwnedKind.Parked, "budget"),
        Subject("item", "ITM-01JH5", "ITM-01JH5"), [Input("item", "Item", "item")],
        Cells(["produce", "checks", "done"], "produce"), "parked: budget", "ApplyCommand:answer-grant-tokens", "ApplyCommand:takeover", "ApplyCommand:cancel");

    // ITM-01JA1 of the screens fixture: taken over at 12:01, at its produce step. Layout 5: the covered state with the badge.
    private static readonly InstanceSummary _takenOver = Row("ITM-01JA1/step/1", _step, "produce", Today(12, 1),
        new EngineOwnedBadge(EngineOwnedKind.TakenOver, "with you since 12:01"),
        Subject("item", "ITM-01JA1", "ITM-01JA1"), [Input("item", "Item", "item")],
        Cells(["produce", "checks", "done"], "produce"), "with you", "ApplyCommand:handback", "ApplyCommand:cancel");

    // A needs-answer park of its own (PRJ-exports is the taken-over project): a project parked at its design desk. Not a row of the root list.
    private static readonly InstanceSummary _prjReports = Row("ITM-01JKE/project/1", _project, "design", Today(10, 30),
        new EngineOwnedBadge(EngineOwnedKind.Parked, "needs-answer"), Subject("linear-project", "ENG", "ENG · Reports"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("design", Shape(("design", c => c with { Desk = true }))), "parked: needs-answer",
        "ApplyCommand:answer-resume", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static readonly InstanceSummary _prjCache = Row("ITM-01JKF/project/1", _project, "scoping", Today(11, 50), new EngineOwnedBadge(EngineOwnedKind.Parked, "spec-conflict (drift)"),
        Subject("linear-project", "ENG", "ENG · Cache layer"), [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", null)],
        ProjectCells("scoping"), "parked: spec-conflict", "ApplyCommand:answer-resume", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static string LinAddress(string team) => team == "ENG" ? "ITM-01JKB/intake-linear/1" : "ITM-01JKC/intake-linear/1";

    private static InstanceSummary Intake(string team, DateTimeOffset since, string now) =>
        Row(LinAddress(team), _intake, "poll", since, null, Subject("linear-team", team, $"Linear {team}"),
            [Input("team", "Linear team", "linear-team")], Cells(_intakeStates, "poll"), now, "ApplyCommand:cancel");

    private static IReadOnlyList<InstanceSummary> Roots() =>
    [
        _inc14, _sentry8812, _prjSync, _prjExports, _prjTabs, _p2211, _sentry8809,
        DoneTriage("ITM-01JK8/triage-alert/1", "sentry #8801", Today(10, 30)),
        DoneTriage("ITM-01JK9/triage-alert/1", "sentry #8797", Today(10, 5)),
        DoneTriage("ITM-01JKA/triage-alert/1", "sentry #8790", Today(9, 40)),
        Intake("ENG", Today(12, 10), "polling every 15m · last 12:15 · 2 new items"),
        Intake("APP", Today(12, 15), "polling every 15m · last 12:15 · 2 new items"),
    ];

    private static ListCounts CountsOf(IReadOnlyList<InstanceSummary> roots) => new(
        roots.Count(r => r.Chip.Word != "DONE"), roots.Count(r => r.Chip.Tone == ChipTone.Attn),
        roots.Count(r => r.Chip.Word == "WITH YOU"), roots.Count(r => r.Chip.Word == "RUNNING"), roots.Count(r => r.Chip.Word == "DONE"));

    // The counts are over the whole root set, before the recipe filter or 'open' narrows the rows (API spec section 3.8).
    private static InstanceList Ordered(IEnumerable<InstanceSummary> rows) =>
        new([.. rows.OrderBy(r => r.Chip.Word == "DONE").ThenBy(IsStuck).ThenBy(r => r.Recipe.Urgency).ThenBy(r => r.Subject.Label, StringComparer.Ordinal)], CountsOf(Roots()), null, Now);

    // Parked and escalated instances sort behind the ones still moving: Needs you carries them, so the Home strip doesn't repeat them.
    private static bool IsStuck(InstanceSummary row) => row.EngineOwned?.Kind is EngineOwnedKind.Parked or EngineOwnedKind.Escalated;

    /// <summary>The Workflows page: the 12 roots, unfinished first, the parked and escalated behind the moving ones, then by urgency and subject label (API spec section 3.8).</summary>
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
    private static StatusFields Status(InstanceSummary row, string? detail, string? next, OpenWork open, StatusLines? lines = null)
    {
        lines ??= new StatusLines();
        var recipe = row.Recipe;
        var header = new StatusHeader(row.Address, recipe.Id, recipe.Hash.Display, recipe.Version, HeaderState(row), detail, row.Since, (long)(Now - row.Since).TotalSeconds);
        var fields = new StatusFields(header, next, lines.Answer, lines.Child, lines.Drift, lines.Inputs, lines.Timeout, lines.Budget, lines.Paused,
            Commands(row, lines.Answer, open), string.Empty);
        return fields with { Text = StatusText.Render(fields, Now) };
    }

    /// <summary>WF section 6.4's state slot: the reserved engine word while the engine owns the instance, else the recipe state.</summary>
    private static string HeaderState(InstanceSummary row) => row.EngineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => "parked",
        EngineOwnedKind.Escalated => "escalated",
        EngineOwnedKind.TakenOver => "taken-over",
        null => row.State,
        _ => throw new InvalidOperationException("no such engine-owned kind"),
    };

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
                "cancel" => (ActionClass.Desk, $"sindri workflow cancel {row.Address.Value}", ConfirmText.Cancel(row.Address.Value, open.RunningSteps, open.OpenChildren)),
                "answer" => (answer!.Class, answer.Cli, null),
                "complete" => (ActionClass.Desk, open.CompleteCli!, null),
                "takeover" => (ActionClass.Pause, $"sindri workflow takeover {row.Address.Value}", null),
                "handback" => (ActionClass.Desk, $"sindri workflow handback {row.Address.Value}", null),
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
        var complete = $"sindri workflow complete {row.Address.Value} --run {run} --outcome done --output fact:{outputKey}={outputSample}";
        return Status(row, "desk task", $"{produce}, then run: {complete}", new OpenWork(CompleteCli: complete), new StatusLines { Inputs = [input] });
    }

    /// <summary>Layout 1: a park that mirrors a parked child; the grant is forwarded to it.</summary>
    public static StatusFields StatusParkedChild() => Status(_prjSearch, "no-progress (child)",
        "Parked (no-progress) in ITM-01JKD/project/1/step.1: checks failed twice with the same findings. Grant more rounds at a terminal, or cancel.",
        new OpenWork(OpenChildren: 1),
        new StatusLines { Answer = Answer(AnswerKind.Grant, "ITM-01JKD/project/1"), Child = new StatusChild(InstanceAddress.From("ITM-01JKD/project/1/step.1"), "parked", "no-progress") });

    /// <summary>Layout 2: an open desk task with its inputs, a parking timeout and the state and instance budgets.</summary>
    public static StatusFields StatusDeskTask() => Status(_sentry8812, "desk task",
        "Produce the Step output for sentry-8812 (round 1/3), then run: sindri workflow complete ITM-01JK2/triage-alert/1 --run 1.1 --outcome done --output fact:severity=<text>",
        new OpenWork(CompleteCli: "sindri workflow complete ITM-01JK2/triage-alert/1 --run 1.1 --outcome done --output fact:severity=<text>"),
        new StatusLines
        {
            Inputs = [_alertInput],
            Timeout = new StatusTimeout(TimeoutKind.Parks, 165_000, "desk"),
            Budget = new StatusBudget("state", 0, 200_000, new BudgetFigures(0, 400_000)),
        });

    /// <summary>Layout 3: a budget park; the answer grants tokens.</summary>
    public static StatusFields StatusParkedBudget() => Status(_budgetStep, "budget", "Parked (budget): produce used 200k of 200k tokens.",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.GrantTokens, "ITM-01JH5/step/1") });

    /// <summary>Layout 4: an escalated gate; the answer accepts at a terminal.</summary>
    public static StatusFields StatusEscalatedGate() => Status(_p2211, "gate",
        "Escalated: gate.severity scored 0.91, above 0.8. Accept to override it (the other checks still run before done), or reject (the Step re-runs within its loop, otherwise this parks).",
        new OpenWork(), new StatusLines { Answer = Answer(AnswerKind.Accept, "ITM-01JK6/triage-alert/1") });

    /// <summary>Layout 5: a taken-over instance with its timeout paused.</summary>
    public static StatusFields StatusTakenOver() => Status(_takenOver, null,
        "With you since 12:01. Run `sindri workflow handback ITM-01JA1/step/1` when you're done.", new OpenWork(), new StatusLines { Paused = new StatusPaused(40) });

    /// <summary>Layout 6: a finished instance, the header only.</summary>
    public static StatusFields StatusDone() => Status(_sentry8809, "resolved", null, new OpenWork());

    /// <summary>A park with a recorded drift: the answer needs the re-approval first, which is its own protected command; the resume stays desk.</summary>
    public static StatusFields StatusParkedDrift() => Status(_prjCache, "spec-conflict (drift)",
        "Parked (spec-conflict, drift): block step.scope.draft changed in this build. Re-approve it at a terminal, then resume.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Resume, "ITM-01JKF/project/1"), Drift = new StatusDrift(BlockId.From("step.scope.draft"), NeedsReapproval: true) });

    /// <summary>A needs-answer wait on a project of its own: the answer is a bare resume with optional text.</summary>
    public static StatusFields StatusNeedsAnswerWait() => Status(_prjReports, "needs-answer",
        "Parked (needs-answer) in design: import the design output, then resume.", new OpenWork(),
        new StatusLines { Answer = Answer(AnswerKind.Resume, "ITM-01JKE/project/1", "--resume --text <answer>") });

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

    // A child is addressed under its parent ('ITM-01JK3/project/1/step.1'); its actions follow its own state.
    private static ChildRow Child(string parent, int entry, string item, string title, StateGlyph glyph, string state, EngineOwnedBadge? owned = null, string? agent = null, long age = 600)
    {
        var actions = glyph == StateGlyph.Done ? Acts("OpenItem")
            : owned is { Kind: EngineOwnedKind.Parked } ? Acts("OpenItem", "ApplyCommand:answer-grant", "ApplyCommand:takeover")
            : Acts("OpenItem", "ApplyCommand:takeover");
        return new(InstanceAddress.From(parent + "/step." + entry.ToString(System.Globalization.CultureInfo.InvariantCulture)), ItemKey.From(item), title, glyph, state, StateChip.For(state, owned, false, state == "done"), null, agent, age, owned, null, actions);
    }

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
        const string parent = "ITM-01JK3/project/1";
        var ws1 = new Workstream("ws-1", "UI",
            [Child(parent, 1, "ITM-01JC3", "Sync status pill", StateGlyph.Done, "done"), Child(parent, 2, "ITM-01JC5", "Conflict resolver dialog", StateGlyph.Done, "done")]);
        var ws2 = new Workstream("ws-2", "Store",
            [Child(parent, 3, "ITM-01JC1", "PUT /sync/changes", StateGlyph.Done, "done"), Child(parent, 4, "ITM-01JC2", "Outbox queue", StateGlyph.Done, "done"),
                Child(parent, 5, "ITM-01JD6", "Services page job panel", StateGlyph.Active, "checks", agent: "factory worker", age: 0)]);
        var park = new EngineOwnedBadge(EngineOwnedKind.Parked, "no-progress");
        var ws3 = new Workstream("ws-3", "Mobile",
            [Child(parent, 6, "ITM-01JD7", "Background sync (iOS)", StateGlyph.Active, "produce", owned: park), Child(parent, 7, "ITM-01JF3", "doctor views line", StateGlyph.Pending, "produce", age: 3_120),
                Child(parent, 8, "ITM-01JF4", "Tray badge on Linux", StateGlyph.Pending, "produce", age: 2_400)]);
        var ws4 = new Workstream("ws-4", "Rollout", [Child(parent, 9, "ITM-01JF8", "Receipt verify route", StateGlyph.Pending, "produce", age: 720)]);
        return Detail(_prjSync, "Offline sync", PathOrdering.Path,
            new CurrentState("build", new StepDeclaration("step.children", "builder.feature", "one child per packet task"),
                [new GateLine(BlockId.From("gate.evidence.all"), "exit", null, "Every child has its evidence pack."), new GateLine(BlockId.From("gate.judge.verify"), "exit", 0.8, "The judge scores the build at least 0.8.")],
                null, status),
            [
                Fact("project", "Linear project", FactType.Link, FactDirection.Input, null, FactStatus.Produced, "Linear APP · Offline sync", At(10, 12, 9, 14), "human:cli",
                    Ref(url: "https://linear.app/example/project/offline-sync", host: "linear.app", title: "APP · Offline sync")),
                Fact("prd", "PRD", FactType.File, FactDirection.Input, null, FactStatus.Produced, "PRD-sync.md", At(10, 12, 9, 14), "human:cli",
                    Ref(name: "PRD-sync.md", size: 18_432, hash: Hash("1c0e5a77"), path: "docs/PRD-sync.md")),
                Fact("scope", "Scope map", FactType.ScopeMap, FactDirection.Output, "scoping", FactStatus.Produced, "4 areas, 9 cited files", At(10, 12, 9, 40), "step.scope.draft", Ref(factKey: "scope")),
                Fact("packet", "Packet", FactType.Packet, FactDirection.Output, "design", FactStatus.Produced, "9 tasks in 4 workstreams, frozen", At(10, 12, 10, 5), "step.packet.freeze", Ref(factKey: "packet")),
                Fact("evidence", "Evidence", FactType.Evidence, FactDirection.Output, "build", FactStatus.InProgress, "4 of 9 packs", null, null, Ref(factKey: "evidence"), new Progress(4, 9)),
            ],
            [new ChildGroup("build", RecipeId.From("workflow.step"), 4, 9, [ws1, ws2, ws3, ws4], [], 0)],
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
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "service:webhook", Ref(factKey: "alert")),
                Fact("severity", "Severity", FactType.Text, FactDirection.Output, "assess", FactStatus.Produced, "sev2", At(10, 12, 11, 4), "step.assess", Ref(factKey: "severity")),
                Fact("postmortem", "Postmortem draft", FactType.Text, FactDirection.Output, "resolved", FactStatus.Pending, "not started"),
            ],
            [new ChildGroup("comms", RecipeId.From("workflow.step"), 0, 1, [], [Child("ITM-01JK1/incident-response/1", 1, "ITM-01JG3", "Post status updates", StateGlyph.Active, "produce", age: 300)], 0)],
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
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8812 TypeError", At(10, 12, 12, 22), "service:webhook", Ref(factKey: "alert")),
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
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "service:webhook", Ref(factKey: "alert")),
                Fact("route", "Route", FactType.Decision, FactDirection.Output, "route", FactStatus.InProgress, "gate.severity 0.91 → INC-14 opened"),
            ],
            [], Counts(6, 1, 0));

    private static InstanceSummary DevRow(string address, string name, InstanceRecipe recipe, string[] states, string state, Func<StateCell, StateCell>? shape, params string[] actions) =>
        Row(address, recipe, state, Today(12, 0), null, Subject("text", name, name), [Input("request", "Request", "text")], Cells(states, state, shape), "next step", actions);

    /// <summary>The desk-round-trip dev recipe, desk open (the P11 signal fixture).</summary>
    public static InstanceDetail DeskRoundTrip()
    {
        var row = DevRow("ITM-01JL1/dev-desk-round-trip/1", "desk-round-trip", _devDesk, _devDeskStates, "desk", Shape(("desk", c => c with { Desk = true })), "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
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
        var row = DevRow("ITM-01JL2/dev-triage-three-state/1", "triage-three-state", _devTriage, _devTriageStates, "classify", null, "ApplyCommand:takeover", "ApplyCommand:cancel");
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
        var row = Row("ITM-01JL3/fork-demo/1", _fork, "right", Today(12, 0), null, Subject("text", "fork-demo", "Fork demo"),
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
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8809 429s on stream", At(10, 12, 11, 40), "service:webhook", Ref(factKey: "alert")),
                Fact("item", "Item", FactType.Item, FactDirection.Output, "route", FactStatus.Produced, "ITM-01JF0 queued", At(10, 12, 11, 58), "step.route", Ref(item: "ITM-01JF0")),
            ],
            [], Counts(7, 0, 0));

    /// <summary>A workflow.step child at produce, round 2 of 3, with its parent (red stub).</summary>
    public static InstanceDetail ItemProduce() => throw new NotSupportedException("red: item-produce");

    /// <summary>A project whose build state has more children than the first 20 rows (red stub).</summary>
    public static InstanceDetail ProjectManyChildren() => throw new NotSupportedException("red: project-many-children");

    // ---- the recipe additions (E-22) ---------------------------------------------------------------------------------

    private static RecipeStateView[] StateViews(string[] states) => [.. states.Select((s, i) => new RecipeStateView(s, i + 1, null))];

    /// <summary>workflow.project: a Linear project input, outputs with their producing states, urgency work.</summary>
    public static RecipeView ProjectRecipe() => new(
        [new RecipeInputView("project", "Linear project", FactType.Link, "linear-project", true), new RecipeInputView("prd", "PRD", FactType.File, null, true)],
        [new RecipeOutputView("scope", "Scope map", FactType.ScopeMap, "scoping"), new RecipeOutputView("packet", "Packet", FactType.Packet, "design"), new RecipeOutputView("evidence", "Evidence", FactType.Evidence, "build")],
        RecipeUrgency.Work, StateViews(_projectStates), [new RecipeLoopView("challenge", "scoping", "scoping")]);

    /// <summary>incident.response: urgency incident and the mitigate and verify loop.</summary>
    public static RecipeView IncidentRecipe() => new(
        [new RecipeInputView("alert", "Alert", FactType.Payload, "webhook", true)],
        [new RecipeOutputView("severity", "Severity", FactType.Text, "assess"), new RecipeOutputView("postmortem", "Postmortem draft", FactType.Text, "resolved")],
        RecipeUrgency.Incident, StateViews(_incidentStates), [new RecipeLoopView("remediate", "mitigate", "verify")]);

    /// <summary>A recipe with no outputs: 'outputs: []', and a kind on the first input only.</summary>
    public static RecipeView NoOutputsRecipe() => new(
        [new RecipeInputView("request", "Request", FactType.Text, "text", true), new RecipeInputView("context", null, FactType.File, null, false)],
        [], RecipeUrgency.Work, StateViews(_devTriageStates), []);

    // ---- the facts route (GetInstanceFact) ---------------------------------------------------------------------------

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, text, payload or truncated (see <see cref="FactFixtures"/>).</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => FactFixtures.Fact(scenario);
}
