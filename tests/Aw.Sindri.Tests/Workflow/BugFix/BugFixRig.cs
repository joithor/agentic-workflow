using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.BugFix;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>An instance with a started bug fix in its run area, bugfix-state in memory, and the contexts the four Steps run with.</summary>
internal sealed class BugFixRig
{
    internal const string Address = "T-1/bugfix/1";

    internal const string Check = """{"kind":"test","file":"tests/bug.test.sh","command":["bash","tests/bug.test.sh"],"directory":"."}""";

    internal const string Evidence = """{"summary":"the api returns the cookie","kind":"api"}""";

    internal BugFixRig()
    {
        StateDirectory = Area.PathFor(Address, "bugfix").Value;
        _ = State.Init(StateDirectory);
    }

    internal InMemoryBugFixState State { get; } = new();

    internal InMemoryRunArea Area { get; } = new();

    internal string StateDirectory { get; }

    internal static StepContext Context(string step, params (string Key, string Json)[] facts) =>
        BuildShipContexts.Of(step, facts.ToDictionary(static f => f.Key, static f => f.Json, StringComparer.Ordinal)) with { Address = Address };

    internal static JsonNode? Out(BlockStepResult result, string key) => result.Result!.Outputs.TryGetValue(FactKey.From("fact:" + key), out var fact) ? JsonNode.Parse(fact.GetRawText()) : null;

    internal static string Decision(string choice) => $$"""{"choice":"{{choice}}","provider":"desk","actor":"owner"}""";

    internal static string Diff(string branch) => $$"""{"branch":"{{branch}}","worktree":"/work/{{branch}}"}""";
}
