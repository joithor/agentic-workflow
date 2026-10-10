using System.Text.RegularExpressions;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The rules on what the packet says: evidence per example, settled questions, and the held-out share.</summary>
internal static partial class ContentChecks
{
    internal static void Run(Packet packet, CheckList checks)
    {
        var examples = packet.Tasks.Tasks.SelectMany(static t => t.Acceptance.Select(e => (Task: t, Example: e))).ToList();
        Examples(examples, checks);
        checks.Rule(
            PacketRules.Decisions,
            "every question is answered or deferred with an owner and a default",
            packet.Decisions.Questions.Where(static q => !Settled(q)).Select(static q => (
                $"question {q.Id.Value}",
                q.Deferred is null ? "is open: no answer and no deferral" : "is deferred without an owner or a 'buildMayAssume' default",
                "Answer it at the desk, or defer it with an owner and what the build may assume meanwhile.")));
        Holdouts(examples, checks);
    }

    private static void Examples(List<(PacketTask Task, AcceptanceExample Example)> examples, CheckList checks)
    {
        checks.Rule(
            PacketRules.AcceptanceText,
            "every visible acceptance example has text",
            examples.Where(static x => !x.Example.Holdout && string.IsNullOrWhiteSpace(x.Example.Text)).Select(static x => (
                $"example {x.Example.Id.Value}",
                $"task {x.Task.Id.Value}'s example has no text",
                "Write the example (given-when-then or prose), or mark it 'holdout: true' if the implementer must not see it.")));
        checks.Rule(
            PacketRules.Evidence,
            "every acceptance example maps to an evidence kind",
            examples
                .Where(static x => x.Example.Evidence.Count == 0 || !x.Example.Evidence.All(static k => EvidenceKind.IsMatch(k)))
                .Select(static x => (
                    $"example {x.Example.Id.Value}",
                    x.Example.Evidence.Count == 0
                        ? $"task {x.Task.Id.Value}'s example names no evidence kind"
                        : $"task {x.Task.Id.Value}'s example names an evidence kind that isn't kebab-case: {string.Join(", ", x.Example.Evidence)}",
                    "Name at least one evidence kind (addendum §17.11), for example 'test' or 'cli-transcript'.")));
    }

    private static void Holdouts(List<(PacketTask Task, AcceptanceExample Example)> examples, CheckList checks)
    {
        checks.Rule(
            PacketRules.HoldoutExists,
            "every task with acceptance examples holds one out",
            examples.GroupBy(static x => x.Task)
                .Where(static g => !g.Any(static x => x.Example.Holdout))
                .Select(static g => (
                    $"task {g.Key.Id.Value}",
                    "none of its acceptance examples is held out",
                    "Add a held-out example to this task ('holdout: true', its text kept in sindri-private state).")));
        checks.Rule(
            PacketRules.HoldoutStored,
            "no held-out example keeps its text in the packet",
            examples.Where(static x => x.Example.Holdout && x.Example.Text is not null).Select(static x => (
                $"example {x.Example.Id.Value}",
                $"task {x.Task.Id.Value}'s held-out example has its text in the packet, where the implementer can read it",
                "Keep only the example's id and evidence kinds here; its text belongs in sindri-private state (addendum §17.19).")));
    }

    private static bool Settled(Question question) =>
        !string.IsNullOrWhiteSpace(question.Answer)
        || (question.Deferred is { } d && !string.IsNullOrWhiteSpace(d.Owner) && !string.IsNullOrWhiteSpace(d.BuildMayAssume));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EvidenceKind { get; }
}
