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
            GateResult(gate),
            Scopes(gate, facts.PlannedScopes),
            Head(facts),
            new MergeCheck("main", gate.Main == facts.CurrentMain, $"gate ran on main {gate.Main}, main is {facts.CurrentMain}"),
            Order(facts),
            new MergeCheck("draft", !facts.IsDraft, facts.IsDraft ? "the pull request is a draft" : "ready for review"),
        ];
    }

    private static MergeCheck GateResult(GateRecord? gate)
    {
        if (gate is null)
        {
            return new MergeCheck("gate-result", Ok: false, NoGate);
        }

        if (gate.Verdict == GateVerdict.Pass)
        {
            return new MergeCheck("gate-result", Ok: true, "PASS");
        }

        var red = gate.Steps.Where(s => s.Verdict != GateVerdict.Pass).ToList();
        var unexplained = red.Where(s => s.Flake != FlakeVerdict.Flake).Select(s => s.Name).ToList();
        return gate.Verdict == GateVerdict.Fail && red.Count > 0 && unexplained.Count == 0
            ? new MergeCheck("gate-result", Ok: true, "every red step is a classified flake")
            : new MergeCheck("gate-result", Ok: false, $"{gate.Verdict}: {(unexplained.Count == 0 ? "no failing step recorded" : "not a flake: " + string.Join(", ", unexplained))}");
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
