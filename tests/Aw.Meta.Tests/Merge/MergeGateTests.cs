using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Merge;

public sealed class MergeGateTests
{
    private static GateRecord Gate(GateVerdict verdict = GateVerdict.Pass, params GateStep[] steps) =>
        new(190, "head1", "main1", ["repo"], steps, verdict, A.At(0));

    private static MergeFacts Facts(GateRecord? gate) => new()
    {
        Pr = 190,
        Gate = gate,
        CurrentHead = "head1",
        CurrentMain = "main1",
        OpenPrs = [190],
        DocsOnly = ["**/*.md", "docs/**"],
    };

    private static string[] Failed(MergeFacts facts) => [.. MergeGate.Evaluate(facts).Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public void A_passed_gate_on_unchanged_head_and_main_holds_every_check()
    {
        var checks = MergeGate.Evaluate(Facts(Gate()));

        Assert.Equal(["gate", "gate-result", "head", "main", "order", "draft"], checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.All(checks, c => Assert.True(c.Ok, c.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_gate_fails_the_gate_checks() =>
        Assert.Equal(["gate", "gate-result", "head", "main"], Failed(Facts(gate: null)), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_failed_gate_fails_unless_every_red_step_is_a_flake()
    {
        var real = new GateStep("unit", GateVerdict.Fail, "/l") { Flake = FlakeVerdict.Real };
        var flake = new GateStep("integration", GateVerdict.Fail, "/l") { Flake = FlakeVerdict.Flake };
        var unclassified = new GateStep("build", GateVerdict.Fail, "/l");
        var green = new GateStep("repo", GateVerdict.Pass, "/l");

        Assert.Equal(["gate-result"], Failed(Facts(Gate(GateVerdict.Fail, green, real, flake))), StringComparer.Ordinal);
        Assert.Equal(["gate-result"], Failed(Facts(Gate(GateVerdict.Fail, unclassified))), StringComparer.Ordinal);
        Assert.Empty(Failed(Facts(Gate(GateVerdict.Fail, green, flake))));
        Assert.Equal(["gate-result"], Failed(Facts(Gate(GateVerdict.Conflict))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_fails_unless_the_delta_is_docs_only_and_the_repo_gate_passed()
    {
        var moved = Facts(Gate()) with { CurrentHead = "head2" };

        Assert.Equal(["head"], Failed(moved), StringComparer.Ordinal);
        Assert.Equal(["head"], Failed(moved with { DeltaPaths = ["README.md"] }), StringComparer.Ordinal);
        Assert.Equal(["head"], Failed(moved with { DeltaPaths = ["README.md", "src/a.cs"], RepoGatePassedOnHead = true }), StringComparer.Ordinal);
        Assert.Empty(Failed(moved with { DeltaPaths = ["README.md", "docs/a/b.png"], RepoGatePassedOnHead = true }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_moved_main_fails() =>
        Assert.Equal(["main"], Failed(Facts(Gate()) with { CurrentMain = "main2" }), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Merge_order_holds_a_pull_request_back_while_its_predecessor_is_open()
    {
        var rules = new[] { new MergeOrderRule(Before: 188, After: 190) };
        var facts = Facts(Gate()) with { MergeOrder = rules };

        Assert.Equal(["order"], Failed(facts with { OpenPrs = [188, 190] }), StringComparer.Ordinal);
        Assert.Empty(Failed(facts with { OpenPrs = [190] }));
        Assert.Empty(Failed(facts with { Pr = 188, OpenPrs = [188, 190] }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_fails() =>
        Assert.Equal(["draft"], Failed(Facts(Gate()) with { IsDraft = true }), StringComparer.Ordinal);
}
