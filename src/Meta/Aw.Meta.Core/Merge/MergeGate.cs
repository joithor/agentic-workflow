using System.Globalization;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Core.Merge;

/// <summary>The merge preconditions.</summary>
public static class MergeGate
{
    private const string NoGate = "no gate recorded for this pull request";

    /// <summary>Evaluates every precondition: gate, gate-result, head, main, order, draft. With no gate recorded, only
    /// the gate check: the pull request isn't looked up then, so order and draft would report defaults, not facts.</summary>
    /// <param name="facts">What is known about the pull request and main.</param>
    /// <returns>One check per precondition, in that order.</returns>
    public static IReadOnlyList<MergeCheck> Evaluate(MergeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var gate = facts.Gate;
        if (gate is null)
        {
            return [new MergeCheck("gate", Ok: false, NoGate + "; run aw-meta gate-run " + facts.Pr.ToString(CultureInfo.InvariantCulture))];
        }

        return
        [
            new MergeCheck("gate", Ok: true, "gate run " + gate.Run.ToString(CultureInfo.InvariantCulture) + " at " + gate.Head),
            GateResult(gate, facts.FailingTests, facts.FailureCounts),
            Scopes(gate, facts.PlannedScopes, facts.ScopeRuns),
            Head(facts),
            Main(gate, facts),
            Order(facts),
            new MergeCheck("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review"),
        ];
    }

    private static MergeCheck GateResult(GateRecord gate, IReadOnlyDictionary<string, IReadOnlyList<string>> failing, IReadOnlyDictionary<string, (int Summarized, int Named)> counts)
    {
        if (gate.Verdict == GateVerdict.Pass)
        {
            return new MergeCheck("gate-result", Ok: true, "PASS");
        }

        var red = gate.Steps.Where(s => s.Verdict != GateVerdict.Pass).ToList();
        if (gate.Verdict != GateVerdict.Fail || red.Count == 0)
        {
            return new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: no failing step recorded");
        }

        var unexplained = red.Select(step => Unexplained(step, failing, counts)).Where(text => text is not null).ToList();
        return unexplained.Count == 0
            ? new MergeCheck("gate-result", Ok: true, "every failing test of every red step is a classified flake")
            : new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: {string.Join("; ", unexplained)}");
    }

    // A red step is accepted only when its log names its failing tests and every one of them was rerun and classified a flake.
    private static string? Unexplained(GateStep step, IReadOnlyDictionary<string, IReadOnlyList<string>> failing, IReadOnlyDictionary<string, (int Summarized, int Named)> counts)
    {
        if (!failing.TryGetValue(step.Name, out var tests) || tests.Count == 0)
        {
            return $"{step.Name}: could not list its failing tests from the log, so no flake classification can vouch for it";
        }

        if (counts.TryGetValue(step.Name, out var count) && count.Summarized != count.Named)
        {
            return $"{step.Name}: the log's summary counts {count.Summarized.ToString(CultureInfo.InvariantCulture)} failed but only {count.Named.ToString(CultureInfo.InvariantCulture)} name a test (a crash or a timeout leaves no name), so no flake classification can vouch for it";
        }

        var open = tests.Where(test => !step.Reruns.Any(r => r.Verdict == FlakeVerdict.Flake && TestNames.Matches(r.Test, test))).ToList();
        return open.Count == 0 ? null : $"not a flake in {step.Name}: {string.Join(", ", open)}";
    }

    // A gate run with fewer scopes than the diff needs (gate-run <pr> repo on a code change) vouches only for what it ran.
    private static MergeCheck Scopes(GateRecord gate, IReadOnlyList<string>? planned, IReadOnlyDictionary<string, int>? runs)
    {
        var ran = string.Join(", ", gate.Scopes.Select(scope => runs is not null && runs.TryGetValue(scope, out var run) ? $"{scope} (run {run.ToString(CultureInfo.InvariantCulture)})" : scope));
        if (planned is null)
        {
            return new MergeCheck("scopes", Ok: false, $"could not work out the scopes the diff needs; the gate ran {ran}");
        }

        var missing = planned.Where(scope => !gate.Scopes.Contains(scope, StringComparer.Ordinal)).ToList();
        return missing.Count == 0
            ? new MergeCheck("scopes", Ok: true, $"ran every scope the diff needs ({ran})")
            : new MergeCheck("scopes", Ok: false, $"the diff needs {string.Join(", ", missing)}; the gate ran {ran}");
    }

    private static MergeCheck Head(MergeFacts facts)
    {
        if (facts.Gate is not { } gate)
        {
            return new MergeCheck("head", Ok: false, NoGate);
        }

        if (gate.Head == facts.CurrentHead)
        {
            return new MergeCheck("head", Ok: true, "head unchanged since the gate");
        }

        var docsOnly = facts.DeltaPaths is { } delta
            && delta.All(path => facts.DocsOnly.Any(glob => GlobMatcher.IsMatch(glob, path)));
        return docsOnly && facts.RepoGatePassedOnHead
            ? new MergeCheck("head", Ok: true, "head moved by a docs-only change and the repo gate passed on it")
            : new MergeCheck("head", Ok: false, $"head moved from {gate.Head} to {facts.CurrentHead} since the gate");
    }

    // The fast path (same main) holds for an unstacked gate only: a stacked gate tested a main plus other pull requests' heads,
    // so it is valid only while merging its head onto main today still gives the tested tree. The same test lets a gate
    // whose main moved stand. A gate recorded before the tree was kept (Tree null) keeps the old rule.
    private static MergeCheck Main(GateRecord gate, MergeFacts facts)
    {
        if (gate.Stack.Count == 0 && gate.Main == facts.CurrentMain)
        {
            return new MergeCheck("main", Ok: true, $"gate ran on main {gate.Main}");
        }

        if (gate.Tree is null)
        {
            return new MergeCheck("main", Ok: false, $"gate ran on main {gate.Main}, main is {facts.CurrentMain}");
        }

        if (facts.MergedTree == gate.Tree)
        {
            return new MergeCheck("main", Ok: true, gate.Stack.Count == 0
                ? $"main moved from {gate.Main} to {facts.CurrentMain}, and the gated head merges onto it to the tested tree"
                : $"the gated head merges onto main {facts.CurrentMain} to the tested tree (the gate stacked {string.Join(", ", gate.Stack.Select(s => "#" + s.Pr.ToString(CultureInfo.InvariantCulture)))})");
        }

        return new MergeCheck("main", Ok: false, gate.Stack.Count == 0
            ? $"gate ran on main {gate.Main}, main is {facts.CurrentMain}, and the gated head no longer merges onto it to the tested tree"
            : $"the gate stacked {string.Join(", ", gate.Stack.Select(s => "#" + s.Pr.ToString(CultureInfo.InvariantCulture)))}; merging the gated head onto main {facts.CurrentMain} does not give the tested tree");
    }

    private static MergeCheck Order(MergeFacts facts)
    {
        var waiting = facts.MergeOrder
            .Where(rule => rule.After == facts.Pr && facts.OpenPrs.Contains(rule.Before))
            .Select(rule => "#" + rule.Before.ToString(CultureInfo.InvariantCulture))
            .ToList();
        return waiting.Count == 0
            ? new MergeCheck("order", Ok: true, "no earlier pull request is waiting")
            : new MergeCheck("order", Ok: false, "must merge after " + string.Join(", ", waiting));
    }
}
