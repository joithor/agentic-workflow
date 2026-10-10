using System.Text.RegularExpressions;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The rules on what the packet says: evidence per example, settled questions, and the held-out share.</summary>
internal static partial class ContentChecks
{
    internal static void Run(Packet packet, CheckList checks)
    {
        var tasks = packet.Tasks.Tasks;
        checks.Rule(
            PacketRules.Evidence,
            "every acceptance example maps to an evidence kind",
            tasks.SelectMany(static t => t.Acceptance.Select(e => (Task: t, Example: e)))
                .Where(static x => x.Example.Evidence.Count == 0 || !x.Example.Evidence.All(static k => EvidenceKind.IsMatch(k)))
                .Select(static x => (
                    $"example {x.Example.Id.Value}",
                    x.Example.Evidence.Count == 0
                        ? $"task {x.Task.Id.Value}'s example names no evidence kind"
                        : $"task {x.Task.Id.Value}'s example names an evidence kind that isn't kebab-case: {string.Join(", ", x.Example.Evidence)}",
                    "Name at least one evidence kind (addendum §17.11), for example 'test' or 'cli-transcript'.")));
        checks.Rule(
            PacketRules.Decisions,
            "every question is answered or deferred with an owner and a default",
            packet.Decisions.Questions.Where(static q => !Settled(q)).Select(static q => (
                $"question {q.Id.Value}",
                q.Deferred is null ? "is open: no answer and no deferral" : "is deferred without an owner or a 'buildMayAssume' default",
                "Answer it at the desk, or defer it with an owner and what the build may assume meanwhile.")));
        checks.Rule(
            PacketRules.HoldoutExists,
            "the packet has a held-out share",
            tasks.SelectMany(static t => t.Acceptance).Any(static e => e.Holdout)
                ? []
                : [("packet", "no acceptance example is held out", "Mark at least one acceptance example 'holdout: true'.")]);
    }

    private static bool Settled(Question question) =>
        !string.IsNullOrWhiteSpace(question.Answer)
        || (question.Deferred is { } d && !string.IsNullOrWhiteSpace(d.Owner) && !string.IsNullOrWhiteSpace(d.BuildMayAssume));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EvidenceKind { get; }
}
