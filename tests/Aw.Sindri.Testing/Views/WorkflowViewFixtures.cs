#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// The wire shapes of API spec section 3.8 (E-20, E-22, E-23) filled from the screens fixture (2026-10-12, now 12:26)
/// and the two dev recipes' planned state lists. The golden files are these values serialized; the UI lanes build
/// against them. Children are partial until engine P7 (no agent runtime rows, workstreams only on PRJ-sync).
/// </summary>
public static class WorkflowViewFixtures
{
    /// <summary>The fixture's now.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 26, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset Today(int hour, int minute) => At(10, 12, hour, minute);

    private static string Hash(string prefix) => prefix.PadRight(64, '0');

    private static InstanceRecipe Recipe(string id, string hashPrefix, int version, RecipeUrgency urgency) =>
        new(id, Hash(hashPrefix), version, urgency);

    private static readonly InstanceRecipe _incident = Recipe("incident.response", "5d90e1b2", 4, RecipeUrgency.Incident);
    private static readonly InstanceRecipe _triage = Recipe("triage.alert", "d41f2a07", 2, RecipeUrgency.Triage);
    private static readonly InstanceRecipe _project = Recipe("workflow.project", "a8c21e7f", 12, RecipeUrgency.Work);
    private static readonly InstanceRecipe _intake = Recipe("intake.linear", "0b7e44d3", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _devTriage = Recipe("workflow.dev-triage-three-state", "e2d7a5b1", 1, RecipeUrgency.Triage);
    private static readonly InstanceRecipe _devDesk = Recipe("workflow.dev-desk-round-trip", "93b5c0f4", 1, RecipeUrgency.Work);
    private static readonly InstanceRecipe _fork = Recipe("workflow.fork-demo", "1f08c6a9", 1, RecipeUrgency.Work);

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
            var cell = new StateCell(states[i], i, null, glyph, null, null, [], false, false, null, null, terminal);
            cells.Add(shape is null ? cell : shape(cell));
        }

        return cells;
    }

    private static Func<StateCell, StateCell> Shape(params (string State, Func<StateCell, StateCell> Change)[] changes) =>
        cell => changes.Where(c => c.State == cell.State).Aggregate(cell, (acc, c) => c.Change(acc));

    private static SubjectRef Subject(string kind, string key, string label, string? href = null) => new(kind, key, label, href);

    private static InputPresence Input(string key, string label, string kind, bool present = true) => new(key, label, kind, true, present);

    private static StatusHeader Header(string address, string recipe, string hashPrefix, int version, string state, string? detail, DateTimeOffset since) =>
        new(address, recipe, hashPrefix.PadRight(12, '0'), version, state, detail, since, (long)(Now - since).TotalSeconds);

    private static StatusCommand Cmd(string verb, string cli) =>
        new(verb, $"ApplyCommand:{verb}", verb is "takeover" or "cancel" ? "protected" : "desk", cli, verb == "cancel" ? Cancel(cli[(cli.LastIndexOf(' ') + 1)..]) : null);

    // N and M come from the fixture until engine P4b exposes the open run and children: PRJ-sync has five open children.
    private static string Cancel(string address) => ConfirmText.Cancel(address, 1, address.EndsWith("PRJ-sync", StringComparison.Ordinal) ? 5 : 0);

    // ---- the instance list (E-20) ------------------------------------------------------------------------------------

    private static InstanceSummary Row(string address, string? item, InstanceRecipe recipe, string state, DateTimeOffset since,
        EngineOwnedBadge? owned, SubjectRef subject, InputPresence[] inputs, List<StateCell> states, string now, params string[] actions) =>
        new(address, IdOf(address), item, recipe, state,
            StateChip.For(state, owned, states.Exists(c => c.Desk && c.Glyph == StateGlyph.Active), states.Exists(c => c.Terminal && c.Glyph == StateGlyph.Done && c.State == state)),
            since, owned, subject, inputs, states, now.Length > 80 ? now[..79] + "…" : now, null, actions);

    /// <summary>The engine's WFI id of an address (a stable stand-in for the fixture).</summary>
    private static string IdOf(string address) =>
        "WFI-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address)))[..10];

    private static InstanceSummary DoneTriage(string address, string label, DateTimeOffset since) =>
        Row(address, null, _triage, "done", since, null, Subject("webhook", address, label),
            [Input("alert", "Alert", "webhook")], Cells(_triageStates, "done"), "done", []);

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
        "accept at a terminal", "ApplyCommand:answer", "ApplyCommand:takeover", "ApplyCommand:cancel") with
    { Answer = StatusEscalatedGate().Answer, };

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
        "with you: import the design output", "ApplyCommand:handback", "ApplyCommand:cancel") with
    { Answer = StatusNeedsAnswerWait().Answer, };

    private static readonly InstanceSummary _prjTabs = Row("workflow/PRJ-tabs", null, _project, "scoping", Today(11, 40), null,
        Subject("linear-project", "ENG", "ENG · Tab manager v2"),
        [Input("project", "Linear project", "linear-project"), Input("prd", "PRD", "file")],
        Cells(_projectStates, "scoping", Shape(("scoping", c => c with { Round = new Round(2, 3), Loops = [new LoopPair("scoping", 2, 3)] }))),
        "challenger round 2 · 84k / 600k", "ApplyCommand:takeover", "ApplyCommand:cancel");

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

    // The counts are over the whole root set, before a filter narrows the rows.
    private static InstanceList Ordered(IEnumerable<InstanceSummary> rows) =>
        new([.. rows.OrderBy(r => r.Recipe.Urgency).ThenBy(r => r.Since)], CountsOf(Roots()), null, Now);

    /// <summary>The Workflows page: the 12 roots in urgency order, then oldest since.</summary>
    public static InstanceList RootList() => Ordered(Roots());

    /// <summary>The roots of recipe triage.alert.</summary>
    public static InstanceList RecipeFilter() => Ordered(Roots().Where(r => r.Recipe.Name == "triage.alert"));

    /// <summary>The open roots ('?root=true&amp;open=true'): the rows not finished, counts unchanged.</summary>
    public static InstanceList OpenList() => Ordered(Roots().Where(r => r.Chip.Word != "DONE"));

    /// <summary>No instances.</summary>
    public static InstanceList EmptyList() => new([], new ListCounts(0, 0, 0, 0, 0), InstanceList.EmptyText, Now);

    // ---- the status layouts (E-23, WF section 6.4) -------------------------------------------------------------------

    /// <summary>Layout 1: a park that mirrors a parked child; the grant is forwarded to it.</summary>
    public static StatusFields StatusParkedChild() => new(
        Header("workflow/PRJ-sync", "workflow.project", "a8c21e7f", 12, "build", "parked: no-progress (child)", Today(11, 54)),
        "Parked (no-progress) in workflow/PRJ-sync/ITM-01JD7: checks failed twice with the same findings. Grant more rounds at a terminal, or cancel.",
        new StatusAnswer(AnswerKind.Grant, "ApplyCommand:answer", "protected", "sindri workflow answer workflow/PRJ-sync --grant 1", AtTerminal: true),
        new StatusChild("workflow/PRJ-sync/ITM-01JD7", "parked", "no-progress"), null, [], null, null, null,
        [Cmd("answer", "sindri workflow answer workflow/PRJ-sync --grant 1"), Cmd("takeover", "sindri workflow takeover workflow/PRJ-sync"), Cmd("cancel", "sindri workflow cancel workflow/PRJ-sync")],
        "workflow/PRJ-sync  workflow.project@a8c21e7f0000  v12  parked: no-progress (child)  since 11:54 (32m)\n"
        + "  next      Parked (no-progress) in workflow/PRJ-sync/ITM-01JD7: checks failed twice with the same findings.\n"
        + "            Grant more rounds at a terminal, or cancel.\n"
        + "  child     workflow/PRJ-sync/ITM-01JD7  parked: no-progress\n"
        + "  answer    sindri workflow answer workflow/PRJ-sync --grant 1      (at a terminal; forwarded to the child)\n"
        + "  commands  answer (forwarded), takeover, cancel\n");

    /// <summary>Layout 2: an open desk task with its inputs, a parking timeout and the state and instance budgets.</summary>
    public static StatusFields StatusDeskTask() => new(
        Header("workflow/sentry-8812/classify", "workflow.step", "9ab2d40c", 4, "classify", "desk task", Today(12, 22)),
        "Produce the Step output for sentry-8812 (round 1/3), then run: sindri workflow complete workflow/sentry-8812/classify --run 1.1 --outcome done --output fact:severity=<text>",
        null, null, null, [new StatusInput("fact:alert", "alert.json", "1c0e5a77")],
        new StatusTimeout(TimeoutKind.Parks, 165_000, "desk"), new StatusBudget("state", 0, 200_000, new BudgetFigures(0, 400_000)), null,
        [Cmd("complete", "sindri workflow complete workflow/sentry-8812/classify --run 1.1 --outcome done --output fact:severity=<text>"), Cmd("takeover", "sindri workflow takeover workflow/sentry-8812/classify"), Cmd("cancel", "sindri workflow cancel workflow/sentry-8812/classify")],
        "workflow/sentry-8812/classify  workflow.step@9ab2d40c0000  v4  classify: desk task  since 12:22 (4m)\n"
        + "  next      Produce the Step output for sentry-8812 (round 1/3), then run:\n"
        + "            sindri workflow complete workflow/sentry-8812/classify --run 1.1 --outcome done --output fact:severity=<text>\n"
        + "  inputs    fact:alert  alert.json  sha256:1c0e…\n"
        + "  timeout   parks in 1d 22h 48m (desk)\n"
        + "  budget    tokens 0 / 200k (state), 0 / 400k (instance)\n"
        + "  commands  complete, takeover, cancel\n");

    /// <summary>Layout 3: a budget park; the answer grants tokens.</summary>
    public static StatusFields StatusParkedBudget() => new(
        Header("workflow/ITM-01JC8/step", "workflow.step", "9ab2d40c", 9, "produce", "parked: budget", Today(11, 54)),
        "Parked (budget): produce used 200k of 200k tokens.",
        new StatusAnswer(AnswerKind.GrantTokens, "ApplyCommand:answer", "protected", "sindri workflow answer workflow/ITM-01JC8/step --grant-tokens 100k", AtTerminal: true),
        null, null, [], null, null, null,
        [Cmd("answer", "sindri workflow answer workflow/ITM-01JC8/step --grant-tokens 100k"), Cmd("takeover", "sindri workflow takeover workflow/ITM-01JC8/step"), Cmd("cancel", "sindri workflow cancel workflow/ITM-01JC8/step")],
        "workflow/ITM-01JC8/step  workflow.step@9ab2d40c0000  v9  parked: budget  since 11:54 (32m)\n"
        + "  next      Parked (budget): produce used 200k of 200k tokens.\n"
        + "  answer    sindri workflow answer workflow/ITM-01JC8/step --grant-tokens 100k     (at a terminal; once per park)\n"
        + "  commands  answer, takeover, cancel\n");

    /// <summary>Layout 4: an escalated gate; the answer accepts at a terminal.</summary>
    public static StatusFields StatusEscalatedGate() => new(
        Header("workflow/pagerduty-P-2211", "workflow.step", "9ab2d40c", 5, "route", "escalated: gate", Today(9, 10)),
        "Escalated: gate.severity scored 0.91, above 0.8. Accept to override it (the other checks still run before done), or reject (the Step re-runs within its loop, otherwise this parks).",
        new StatusAnswer(AnswerKind.Accept, "ApplyCommand:answer", "protected", "sindri workflow answer workflow/pagerduty-P-2211 --accept", AtTerminal: true),
        null, null, [], null, null, null,
        [Cmd("answer", "sindri workflow answer workflow/pagerduty-P-2211 --accept"), Cmd("takeover", "sindri workflow takeover workflow/pagerduty-P-2211"), Cmd("cancel", "sindri workflow cancel workflow/pagerduty-P-2211")],
        "workflow/pagerduty-P-2211  workflow.step@9ab2d40c0000  v5  escalated: gate  since 09:10 (3h 16m)\n"
        + "  next      Escalated: gate.severity scored 0.91, above 0.8. Accept to override it (the other checks still run\n"
        + "            before done), or reject (the Step re-runs within its loop, otherwise this parks).\n"
        + "  answer    sindri workflow answer workflow/pagerduty-P-2211 --accept     (at a terminal)\n"
        + "  commands  answer, takeover, cancel\n");

    /// <summary>Layout 5: a taken-over instance with its timeout paused.</summary>
    public static StatusFields StatusTakenOver() => new(
        Header("workflow/ITM-01JA1", "workflow.dev-timeout", "77aa01c3", 3, "taken-over", null, Today(12, 1)),
        "With you since 12:01. Run `sindri workflow handback workflow/ITM-01JA1` when you're done.",
        null, null, null, [], null, null, new StatusPaused(40),
        [Cmd("handback", "sindri workflow handback workflow/ITM-01JA1"), Cmd("cancel", "sindri workflow cancel workflow/ITM-01JA1")],
        "workflow/ITM-01JA1  workflow.dev-timeout@77aa01c30000  v3  taken-over  since 12:01 (25m)\n"
        + "  next      With you since 12:01. Run `sindri workflow handback workflow/ITM-01JA1` when you're done.\n"
        + "  paused    timeout 40s left\n"
        + "  commands  handback, cancel\n");

    /// <summary>Layout 6: a finished instance, the header only.</summary>
    public static StatusFields StatusDone() => new(
        Header("workflow/sentry-8809", "triage.alert", "d41f2a07", 2, "done", "resolved", Today(11, 58)),
        string.Empty, null, null, null, [], null, null, null, [],
        "workflow/sentry-8809  triage.alert@d41f2a070000  v2  done: resolved  since 11:58 (28m)\n");

    /// <summary>A park with a recorded drift: the answer needs the re-approval first.</summary>
    public static StatusFields StatusParkedDrift() => new(
        Header("workflow/PRJ-tabs", "workflow.project", "a8c21e7f", 12, "scoping", "parked: spec-conflict", Today(11, 50)),
        "Parked (spec-conflict, drift): block step.scope.draft changed in this build. Re-approve it at a terminal, then resume.",
        new StatusAnswer(AnswerKind.Resume, "ApplyCommand:answer", "protected", "sindri workflow answer workflow/PRJ-tabs --resume", AtTerminal: true),
        null, new StatusDrift("step.scope.draft", NeedsReapproval: true), [], null, null, null,
        [Cmd("answer", "sindri workflow answer workflow/PRJ-tabs --resume"), Cmd("takeover", "sindri workflow takeover workflow/PRJ-tabs"), Cmd("cancel", "sindri workflow cancel workflow/PRJ-tabs")],
        "workflow/PRJ-tabs  workflow.project@a8c21e7f0000  v12  parked: spec-conflict  since 11:50 (36m)\n"
        + "  next      Parked (spec-conflict, drift): block step.scope.draft changed in this build. Re-approve it at a terminal, then resume.\n"
        + "  drift     step.scope.draft changed; the answer needs the re-approval first\n"
        + "  answer    sindri workflow answer workflow/PRJ-tabs --resume     (at a terminal)\n"
        + "  commands  answer, takeover, cancel\n");

    /// <summary>A needs-answer wait: the answer is a bare resume with optional text.</summary>
    public static StatusFields StatusNeedsAnswerWait() => new(
        Header("workflow/PRJ-exports", "workflow.project", "a8c21e7f", 12, "design", "parked: needs-answer", At(10, 11, 16, 2)),
        "Parked (needs-answer) in design: import the design output, then resume.",
        new StatusAnswer(AnswerKind.Resume, "ApplyCommand:answer", "desk", "sindri workflow answer workflow/PRJ-exports --resume --text <answer>", AtTerminal: false),
        null, null, [], null, null, null,
        [Cmd("answer", "sindri workflow answer workflow/PRJ-exports --resume --text <answer>"), Cmd("takeover", "sindri workflow takeover workflow/PRJ-exports"), Cmd("cancel", "sindri workflow cancel workflow/PRJ-exports")],
        "workflow/PRJ-exports  workflow.project@a8c21e7f0000  v12  parked: needs-answer  since 10-11 16:02 (20h 24m)\n"
        + "  next      Parked (needs-answer) in design: import the design output, then resume.\n"
        + "  answer    sindri workflow answer workflow/PRJ-exports --resume --text <answer>\n"
        + "  commands  answer, takeover, cancel\n");

    // ---- facts and children ------------------------------------------------------------------------------------------

    private static FactRow Fact(string key, string label, FactType type, FactDirection direction, string? from, FactStatus status, string summary,
        DateTimeOffset? at = null, string? by = null, FactRef? reference = null, Progress? progress = null, FactPending? pending = null) =>
        new(key, label, type, direction, from, status, summary, at, by, reference, progress, pending);

    private static ChildRow Child(string item, string title, StateGlyph glyph, string state, EngineOwnedBadge? owned = null, EngineOwnedBadge? park = null, Round? round = null, string? agent = null, long age = 600) =>
        new($"workflow/{item}", item, title, glyph, state, StateChip.For(state, owned, false, state == "done"), round, agent, age, owned, park, ["OpenItem", "ApplyCommand:takeover"]);

    private static ViewCounts Counts(int timeline, int decisions, int requests) => new(timeline, decisions, requests, null);

    // ---- the instance views (E-20) -----------------------------------------------------------------------------------

    private static InstanceDetail Detail(InstanceSummary row, string title, PathOrdering ordering, CurrentState current, IReadOnlyList<FactRow> facts,
        IReadOnlyList<ChildGroup> children, ViewCounts counts, params string[] actions) =>
        new(row.Address, row.Instance, null, row.Item, title, row.Recipe, row.State, row.Since, row.EngineOwned, row.Subject, ordering, row.States, current, facts, children, counts, actions, Now);

    /// <summary>PRJ-sync: build, four of nine children done in four workstreams, a scope map, a packet and evidence.</summary>
    public static InstanceDetail ProjectBuild()
    {
        var status = StatusParkedChild() with
        {
            Timeout = new StatusTimeout(TimeoutKind.Escalates, 86_400, "build"),
            Budget = new StatusBudget("state", 1_200_000, 4_000_000, new BudgetFigures(1_900_000, 6_000_000)),
        };
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
                [new GateLine("gate.evidence.all", "exit", null, "Every child has its evidence pack."), new GateLine("gate.judge.verify", "exit", 0.8, "The judge scores the build at least 0.8.")],
                null, status),
            [
                Fact("project", "Linear project", FactType.Link, FactDirection.Input, null, FactStatus.Produced, "Linear APP · Offline sync", At(10, 12, 9, 14), "human:cli",
                    new FactRef(null, null, null, null, null, "https://linear.app/example/project/offline-sync", "linear.app", "APP · Offline sync", null)),
                Fact("prd", "PRD", FactType.File, FactDirection.Input, null, FactStatus.Produced, "PRD-sync.md", At(10, 12, 9, 14), "human:cli",
                    new FactRef("PRD-sync.md", 18_432, Hash("1c0e5a77"), "docs/PRD-sync.md", null, null, null, null, null)),
                Fact("scope", "Scope map", FactType.ScopeMap, FactDirection.Output, "scoping", FactStatus.Produced, "12 areas, 31 cited files", At(10, 12, 9, 40), "step.scope.draft",
                    new FactRef(null, null, null, null, null, null, null, null, "scope")),
                Fact("packet", "Packet 3f1c09", FactType.Packet, FactDirection.Output, "design", FactStatus.Produced, "9 tasks in 4 workstreams, frozen", At(10, 12, 10, 5), "step.packet.freeze",
                    new FactRef(null, null, null, null, null, null, null, null, "packet")),
                Fact("evidence", "Evidence", FactType.Evidence, FactDirection.Output, "build", FactStatus.InProgress, "4 of 9 packs", null, null,
                    new FactRef(null, null, null, null, null, null, null, null, "evidence"), new Progress(4, 9)),
            ],
            [new ChildGroup("build", "builder.feature", 4, 9, [ws1, ws2, ws3, ws4], [], 0)],
            Counts(41, 3, 1), "ApplyCommand:takeover", "ApplyCommand:cancel");
    }

    /// <summary>INC-14: the mitigate and verify loop at round 1 of 3, a recorded sev2 outcome, a running comms child, a timeout that escalates.</summary>
    public static InstanceDetail IncidentLoop()
    {
        var status = StatusDeskTask() with
        {
            Header = Header("workflow/INC-14", "incident.response", "5d90e1b2", 4, "mitigate", "round 1/3", Today(11, 2)),
            Next = "agent rolling back in ITM-01JG2 · comms posting",
            Timeout = new StatusTimeout(TimeoutKind.Escalates, 3_600, "mitigate"),
            Text = "workflow/INC-14  incident.response@5d90e1b20000  v4  mitigate: round 1/3  since 11:02 (1h 24m)\n"
                + "  next      agent rolling back in ITM-01JG2 · comms posting\n"
                + "  timeout   escalates in 1h (mitigate)\n"
                + "  commands  complete, takeover, cancel\n",
        };
        return Detail(_inc14, "Outbox retry storm", PathOrdering.Path,
            new CurrentState("mitigate", new StepDeclaration("step.agent", "mitigate.rollback", "roll back the last release"), [], null, status),
            [
                Fact("alert", "Alert", FactType.Link, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "human:webhook",
                    new FactRef(null, null, null, null, null, "https://pagerduty.example/incidents/P-2211", "pagerduty.example", "P-2211", null)),
                Fact("severity", "Severity", FactType.Decision, FactDirection.Output, "assess", FactStatus.Produced, "sev2", At(10, 12, 11, 4), "step.assess"),
                Fact("postmortem", "Postmortem draft", FactType.Text, FactDirection.Output, "resolved", FactStatus.Pending, "not started"),
            ],
            [new ChildGroup("comms", "builder.comms", 0, 1, [], [Child("ITM-01JG3", "Post status updates", StateGlyph.Active, "post", age: 300)], 0)],
            Counts(12, 1, 0), "ApplyCommand:takeover", "ApplyCommand:cancel");
    }

    /// <summary>sentry #8812: the classify desk is open and asks for a severity.</summary>
    public static InstanceDetail TriageDesk()
    {
        var pending = new FactPending(true, ["output.nonempty"], "CompleteStep");
        return Detail(_sentry8812, "TypeError in tabs.layout", PathOrdering.Path,
            new CurrentState("classify", new StepDeclaration("step.desk", "classify.severity", "a person classifies the alert"),
                [], new DeskForm("1.1", [new DeskOutput("severity", FactType.Text, true)], ["output.nonempty"], "CompleteStep"), StatusDeskTask()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8812 TypeError", At(10, 12, 12, 22), "human:webhook",
                    new FactRef(null, null, null, null, null, null, null, null, "alert")),
                Fact("severity", "Severity", FactType.Text, FactDirection.Output, "classify", FactStatus.Pending, "awaiting your answer", pending: pending),
            ],
            [], Counts(3, 0, 1), "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
    }

    /// <summary>pagerduty P-2211: escalated at the severity gate; the accept needs a terminal.</summary>
    public static InstanceDetail EscalatedGate() =>
        Detail(_p2211, "PagerDuty P-2211", PathOrdering.Path,
            new CurrentState("route", new StepDeclaration("step.tool", "route.severity", "route by severity"),
                [new GateLine("gate.severity", "exit", 0.8, "Severity under 0.8 routes to a ticket; above it opens an incident.")], null, StatusEscalatedGate()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "PagerDuty P-2211", At(10, 12, 11, 2), "human:webhook",
                    new FactRef(null, null, null, null, null, null, null, null, "alert")),
                Fact("route", "Route", FactType.Decision, FactDirection.Output, "route", FactStatus.InProgress, "gate.severity 0.91 → INC-14 opened"),
            ],
            [], Counts(6, 1, 0), "ApplyCommand:answer", "ApplyCommand:takeover", "ApplyCommand:cancel");

    private static InstanceSummary DevRow(string name, InstanceRecipe recipe, string[] states, string state, Func<StateCell, StateCell>? shape, params string[] actions) =>
        Row($"workflow/{name}", null, recipe, state, Today(12, 0), null, Subject("text", name, name), [Input("request", "Request", "text")], Cells(states, state, shape), "next step", actions);

    /// <summary>The desk-round-trip dev recipe, desk open (the P11 signal fixture).</summary>
    public static InstanceDetail DeskRoundTrip()
    {
        var row = DevRow("desk-round-trip-1", _devDesk, _devDeskStates, "desk", Shape(("desk", c => c with { Desk = true })), "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Desk round trip", PathOrdering.Path,
            new CurrentState("desk", new StepDeclaration("step.desk", "desk.produce", "a person produces the file"),
                [], new DeskForm("1.1", [new DeskOutput("report", FactType.File, true)], ["output.file"], "CompleteStep"), StatusDeskTask()),
            [
                Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Summarize the week", At(10, 12, 12, 0), "human:cli"),
                Fact("report", "Report", FactType.File, FactDirection.Output, "desk", FactStatus.Pending, "awaiting your file",
                    pending: new FactPending(true, ["output.file"], "CompleteStep")),
            ],
            [], Counts(2, 0, 0), "CompleteStep", "ApplyCommand:takeover", "ApplyCommand:cancel");
    }

    /// <summary>The three-state triage dev recipe: 'order' follows the transition order.</summary>
    public static InstanceDetail TriageThreeState()
    {
        var row = DevRow("triage-three-state-1", _devTriage, _devTriageStates, "classify", null, "ApplyCommand:takeover", "ApplyCommand:cancel");
        return Detail(row, "Three-state triage", PathOrdering.Path,
            new CurrentState("classify", new StepDeclaration("step.tool", "classify.rules", "classify by rules"), [], null, StatusDeskTask() with { Answer = null }),
            [
                Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Crash on launch", At(10, 12, 12, 0), "human:cli"),
                Fact("label", "Label", FactType.Text, FactDirection.Output, "classify", FactStatus.InProgress, "classifying"),
            ],
            [], Counts(2, 0, 0), "ApplyCommand:takeover", "ApplyCommand:cancel");
    }

    /// <summary>A recipe with two initial paths: the band says transition-list and folds 'right' under 'left'.</summary>
    public static InstanceDetail TransitionList()
    {
        var row = Row("workflow/fork-demo-1", null, _fork, "right", Today(12, 0), null, Subject("text", "fork-demo", "Fork demo"),
            [Input("request", "Request", "text")],
            Cells(_forkStates, "right", Shape(("right", c => c with { BranchOf = "left" }))), "next step", "ApplyCommand:cancel");
        return Detail(row, "Fork demo", PathOrdering.TransitionList,
            new CurrentState("right", new StepDeclaration("step.tool", "branch.right", "the right branch"), [], null, StatusDeskTask() with { Answer = null }),
            [Fact("request", "Request", FactType.Text, FactDirection.Input, null, FactStatus.Produced, "Fork it", At(10, 12, 12, 0), "human:cli")],
            [], Counts(2, 0, 0), "ApplyCommand:cancel");
    }

    /// <summary>sentry #8809, finished: a header-only status and no commands.</summary>
    public static InstanceDetail Done() =>
        Detail(_sentry8809, "429s on stream", PathOrdering.Path,
            new CurrentState("done", null, [], null, StatusDone()),
            [
                Fact("alert", "Alert", FactType.Payload, FactDirection.Input, null, FactStatus.Produced, "sentry #8809 429s on stream", At(10, 12, 11, 40), "human:webhook",
                    new FactRef(null, null, null, null, null, null, null, null, "alert")),
                Fact("item", "Item", FactType.Item, FactDirection.Output, "route", FactStatus.Produced, "ITM-01JF0 queued", At(10, 12, 11, 58), "step.route",
                    new FactRef(null, null, null, null, "ITM-01JF0", null, null, null, null)),
            ],
            [], Counts(7, 0, 0));

    // ---- the recipe additions (E-22) ---------------------------------------------------------------------------------

    private static RecipeStateView[] StateViews(string[] states) => [.. states.Select((s, i) => new RecipeStateView(s, i, null))];

    /// <summary>workflow.project: a Linear project input, outputs with their producing states, urgency work.</summary>
    public static RecipeView ProjectRecipe() => new(
        [new RecipeInputView("project", "Linear project", "text", "linear-project", true), new RecipeInputView("prd", "PRD", "file", null, true)],
        [new RecipeOutputView("scope", "Scope map", FactType.ScopeMap, "scoping"), new RecipeOutputView("packet", "Packet", FactType.Packet, "design"), new RecipeOutputView("evidence", "Evidence", FactType.Evidence, "build")],
        RecipeUrgency.Work, StateViews(_projectStates), []);

    /// <summary>incident.response: urgency incident and the mitigate and verify loop.</summary>
    public static RecipeView IncidentRecipe() => new(
        [new RecipeInputView("alert", "Alert", "text", "webhook", true)],
        [new RecipeOutputView("severity", "Severity", FactType.Decision, "assess")],
        RecipeUrgency.Incident, StateViews(_incidentStates), [new RecipeLoopView("remediate", "mitigate", "verify")]);

    /// <summary>A recipe with no outputs: 'outputs: []', and a kind on the first input only.</summary>
    public static RecipeView NoOutputsRecipe() => new(
        [new RecipeInputView("request", "Request", "text", "text", true), new RecipeInputView("context", null, "file", null, false)],
        [], RecipeUrgency.Work, StateViews(_devTriageStates), []);

    // ---- the facts route (GetInstanceFact) ---------------------------------------------------------------------------

    private static JsonElement Value(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>The body behind one fact row, by scenario: scope-map, packet, evidence, text, payload or truncated.</summary>
    /// <param name="scenario">The scenario name.</param>
    /// <returns>The fact view.</returns>
    public static FactView Fact(string scenario) => scenario switch
    {
        "scope-map" => new FactView("scope", FactType.ScopeMap, Value("""{"areas":[{"name":"sync store","files":["src/sync/store.ts","src/sync/outbox.ts"]},{"name":"status pill","files":["ui/sync-pill.tsx"]}]}"""), false),
        "packet" => new FactView("packet", FactType.Packet, Value("""{"id":"3f1c09","tasks":[{"id":"t1","title":"PUT /sync/changes","workstream":"ws-2","checks":["unit","contract"]},{"id":"t2","title":"Outbox queue","workstream":"ws-2","checks":["unit"]}]}"""), false),
        "evidence" => new FactView("evidence", FactType.Evidence, Value("""{"packs":[{"task":"t1","verdict":"pass"},{"task":"t2","verdict":"pass"}],"done":4,"total":9}"""), false),
        "text" => new FactView("severity", FactType.Text, Value("\"sev2: the outbox dispatcher retries without backoff\""), false),
        "payload" => new FactView("alert", FactType.Payload, Value("""{"source":"sentry","issue":8812,"title":"TypeError in tabs.layout","tags":{"env":"prod"}}"""), false),
        "truncated" => new FactView("alert", FactType.Payload, JsonSerializer.SerializeToElement("{\"events\":[{\"n\":1},{\"n\":2}"), true),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such fact scenario"),
    };
}
