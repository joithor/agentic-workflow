using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// What a lens round's start writes (line spec §5.2): the new round's number, the lens queue, and the lenses the round does not run recorded in 'fact:lensRounds' as
/// 'notApplicable' with the reason, so none is skipped silently. The queue is the lens ids in pack order, the shape 'step.lens.log' takes lenses off.
/// </summary>
internal static class RoundSeed
{
    /// <summary>The round that starts: the last entry's round, or the next one after a round that closed; 1 for an empty log.</summary>
    /// <param name="rounds">The log so far.</param>
    /// <returns>The number, counted from 1.</returns>
    internal static int Next(JsonArray rounds)
    {
        if (rounds.LastOrDefault() is not JsonObject last)
        {
            return 1;
        }

        var round = last["round"] is JsonValue value && value.TryGetValue<int>(out var n) ? n : 1;
        return last["closes"] is JsonValue closes && closes.TryGetValue<bool>(out var flag) && flag ? round + 1 : round;
    }

    /// <summary>The log read from a fact, or the reason it can't be.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The log (a copy), or null with the reason.</returns>
    internal static (JsonArray? Rounds, string? Reason) Log(StepContext context) =>
        StepFacts.Read(context, "fact:lensRounds") is JsonArray rounds ? ((JsonArray)rounds.DeepClone(), null) : (null, "fact:lensRounds is not a JSON array (fix: seed it with [] where the loop starts)");

    /// <summary>The 'ready' result: the queue and the log with the round's not-applicable lenses appended.</summary>
    /// <param name="pack">The pack the lenses came from.</param>
    /// <param name="selection">Which lenses apply.</param>
    /// <param name="rounds">The log so far.</param>
    /// <returns>The result.</returns>
    internal static BlockStepResult Ready(LensPack pack, LensSelection selection, JsonArray rounds)
    {
        var round = Next(rounds);
        foreach (var (lens, reason) in selection.NotApplicable)
        {
            rounds.Add((JsonNode?)new JsonObject { ["lens"] = lens.Value, ["round"] = round, ["notApplicable"] = reason, ["packHash"] = pack.Hash });
        }

        return StepFacts.Done("ready", ("fact:lensQueue", new JsonArray([.. selection.Queued.Select(static id => (JsonNode?)JsonValue.Create(id.Value))])), ("fact:lensRounds", rounds));
    }
}
