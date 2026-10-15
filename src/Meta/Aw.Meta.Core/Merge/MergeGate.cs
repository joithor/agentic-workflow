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
            GateResult(gate, facts.FailingTests),
            Scopes(gate, facts.PlannedScopes),
            Head(facts),
            new MergeCheck("main", gate.Main == facts.CurrentMain, $"gate ran on main {gate.Main}, main is {facts.CurrentMain}"),
            Order(facts),
            new MergeCheck("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review"),
        ];
    }

    private static MergeCheck GateResult(GateRecord gate, IReadOnlyDictionary<string, IReadOnlyList<string>> failing)
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

        var unexplained = red.Select(step => Unexplained(step, failing)).Where(text => text is not null).ToList();
        return unexplained.Count == 0
            ? new MergeCheck("gate-result", Ok: true, "every failing test of every red step is a classified flake")
            : new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: {string.Join("; ", unexplained)}");
    }

    // A red step is accepted only when its log names its failing tests and every one of them was rerun and classified a flake.
    private static string? Unexplained(GateStep step, IReadOnlyDictionary<string, IReadOnlyList<string>> failing)
    {
        if (!failing.TryGetValue(step.Name, out var tests) || tests.Count == 0)
        {
            return $"{step.Name}: could not list its failing tests from the log, so no flake classification can vouch for it";
        }

        var open = tests.Where(test => !step.Reruns.Any(r => r.Verdict == FlakeVerdict.Flake && TestNames.Matches(r.Test, test))).ToList();
        return open.Count == 0 ? null : $"not a flake in {step.Name}: {string.Join(", ", open)}";
    }

    // A gate run with fewer scopes than the diff needs (gate-run <pr> repo on a code change) vouches only for what it ran.
    private static MergeCheck Scopes(GateRecord gate, IReadOnlyList<string>? planned)
    {
        var ran = string.Join(", ", gate.Scopes);
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
