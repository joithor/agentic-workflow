#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>The recipes the fixtures instantiate. The dev recipes and workflow.step mirror their files under recipes/; the others are fixture-only.</summary>
public static class FixtureRecipes
{
    private static ShapeState S(string id, int? order, string? branchOf = null, bool terminal = false, bool desk = false, bool child = false) => new(id, order, branchOf, terminal, desk, child);

    private static ShapeInput In(string key, string? label, FactType type, string? kind) => new(key, label, type, kind);

    private static ShapeOutput Out(string key, string label, FactType type, string from) => new(key, label, type, from);

    /// <summary>workflow.step v1 (recipes/sindri/workflow.step.yaml): produce, checks, verify, done; the revisions loop returns to produce.</summary>
    public static RecipeShape Step { get; } = new("workflow.step", 1, "1b13bd920351e56227141fe24e59f2f90cb46bc3549e1e90f2da2af1c3be9f5c", RecipeUrgency.Work,
        [S("produce", 1), S("checks", 2), S("verify", 3), S("done", 4, terminal: true)],
        [new ShapeLoop("revisions", "checks", "produce", 3), new ShapeLoop("revisions", "verify", "produce", 3)],
        [In("fact:spec", null, FactType.Text, null)], []);

    /// <summary>workflow.dev-desk-round-trip v1: export (desk), import, done; the rounds loop returns from import to export.</summary>
    public static RecipeShape DevDesk { get; } = new("workflow.dev-desk-round-trip", 1, "42e9e8ab897f90e38c74f9a3a5aa499f9a56475b4c1a416c0ad5b01a79bb2346", RecipeUrgency.Work,
        [S("export", 1, desk: true), S("import", 2), S("done", 3, terminal: true)],
        [new ShapeLoop("rounds", "import", "export", 2)],
        [In("fact:request", "Request", FactType.Text, "text")], [Out("fact:output", "Exported file", FactType.File, "export")]);

    /// <summary>workflow.dev-triage-three-state v1: desk, answer, done.</summary>
    public static RecipeShape DevTriage { get; } = new("workflow.dev-triage-three-state", 1, "88a0e8fc810fb8dbdccb244b1a601e16743ade3c4230b57c7c0cf782b3ed0f66", RecipeUrgency.Triage,
        [S("desk", 1, desk: true), S("answer", 2), S("done", 3, terminal: true)], [],
        [In("fact:item", "Item", FactType.Item, "item")],
        [Out("fact:output", "Draft", FactType.File, "desk"), Out("fact:answer", "Decision", FactType.Decision, "answer")]);

    /// <summary>workflow.incident-response (fixture-only): the remediate loop is recorded as its back edge, verify to mitigate.</summary>
    public static RecipeShape Incident { get; } = new("workflow.incident-response", 4, "5d90e1b2", RecipeUrgency.Incident,
        [S("assess", 1), S("mitigate", 2), S("verify", 3), S("comms", 4, child: true), S("resolved", 5, terminal: true)],
        [new ShapeLoop("remediate", "verify", "mitigate", 3)],
        [In("fact:alert", "Alert", FactType.Payload, "webhook")],
        [Out("fact:severity", "Severity", FactType.Text, "assess"), Out("fact:postmortem", "Postmortem draft", FactType.Text, "comms")]);

    /// <summary>workflow.triage-alert (fixture-only): the classify state is a desk.</summary>
    public static RecipeShape Triage { get; } = new("workflow.triage-alert", 2, "d41f2a07", RecipeUrgency.Triage,
        [S("ingest", 1), S("classify", 2, desk: true), S("route", 3), S("done", 4, terminal: true)], [],
        [In("fact:alert", "Alert", FactType.Payload, "webhook")],
        [Out("fact:severity", "Severity", FactType.Text, "classify"), Out("fact:route", "Route", FactType.Decision, "route"), Out("fact:item", "Item", FactType.Item, "route")]);

    /// <summary>workflow.project (fixture-only): the scoping challenge loop, a design desk and a build state that waits on its children.</summary>
    public static RecipeShape Project { get; } = new("workflow.project", 12, "a8c21e7f", RecipeUrgency.Work,
        [S("intake", 1), S("scoping", 2), S("design", 3, desk: true), S("build", 4, child: true), S("verify", 5), S("done", 6, terminal: true)],
        [new ShapeLoop("challenge", "scoping", "scoping", 3)],
        [In("fact:project", "Linear project", FactType.Payload, "linear-project"), In("fact:prd", "PRD", FactType.File, null)],
        [Out("fact:scope", "Scope map", FactType.ScopeMap, "scoping"), Out("fact:packet", "Packet", FactType.Packet, "design"), Out("fact:evidence", "Evidence", FactType.Evidence, "build")]);

    /// <summary>workflow.intake-linear (fixture-only): polls a team and emits items.</summary>
    public static RecipeShape Intake { get; } = new("workflow.intake-linear", 1, "0b7e44d3", RecipeUrgency.Work,
        [S("poll", 1), S("emit", 2), S("done", 3, terminal: true)], [],
        [In("fact:team", "Linear team", FactType.Text, "text")], []);

    /// <summary>workflow.fork-demo (fixture-only): two initial paths, so the band is a transition list.</summary>
    public static RecipeShape Fork { get; } = new("workflow.fork-demo", 1, "1f08c6a9", RecipeUrgency.Work,
        [S("start", 1), S("left", 2), S("right", null, "left"), S("merge", 3), S("done", 4, terminal: true)], [],
        [In("fact:request", "Request", FactType.Text, "text")], []);

    /// <summary>Every shape.</summary>
    public static IReadOnlyList<RecipeShape> All { get; } = [Step, DevDesk, DevTriage, Incident, Triage, Project, Intake, Fork];
}
