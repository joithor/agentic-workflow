using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The one state chip of the screens fixture ("State chips: one mapping of word to colour"): a word and the colour class it takes.</summary>
/// <param name="Word">The chip word, upper case.</param>
/// <param name="Tone">The colour class.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateChip(string Word, ChipTone Tone)
{
    /// <summary>The chip of an instance: an engine-owned badge decides first, then an open desk answer, then not started, terminal and running.</summary>
    /// <param name="engineOwned">The engine-owned badge, if any; a taken-over instance arrives here as the "taken-over" badge (the plan's "takenOver" flag).</param>
    /// <param name="deskOpen">Whether a desk answer is open.</param>
    /// <param name="terminal">Whether the state is final.</param>
    /// <param name="notStarted">Whether the instance hasn't started: it sits at its recipe's first state, waiting for a slot.</param>
    /// <returns>The chip.</returns>
    public static StateChip For(EngineOwnedBadge? engineOwned, bool deskOpen, bool terminal, bool notStarted = false) => engineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => new StateChip("PARKED", ChipTone.Attn),
        EngineOwnedKind.Escalated => new StateChip("ESCALATED", ChipTone.Attn),
        EngineOwnedKind.TakenOver => new StateChip("WITH YOU", ChipTone.Neutral),
        _ when deskOpen => new StateChip("DECISION", ChipTone.Attn),
        _ when notStarted => new StateChip("QUEUED", ChipTone.Neutral),
        _ when terminal => new StateChip("DONE", ChipTone.Neutral),
        _ => new StateChip("RUNNING", ChipTone.Ok),
    };
}
