using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The one state chip of the screens fixture ("State chips: one mapping of word to colour"): a word and the colour class it takes.</summary>
/// <param name="Word">The chip word, upper case.</param>
/// <param name="Tone">The colour class.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateChip(string Word, ChipTone Tone)
{
    /// <summary>The chip of a state: an engine-owned badge decides first, then an open desk answer, then queued, terminal and running.</summary>
    /// <param name="state">The state id, or 'queued' for an instance waiting for a slot.</param>
    /// <param name="engineOwned">The engine-owned badge, if any.</param>
    /// <param name="deskOpen">Whether a desk answer is open.</param>
    /// <param name="terminal">Whether the state is final.</param>
    /// <returns>The chip.</returns>
    public static StateChip For(string state, EngineOwnedBadge? engineOwned, bool deskOpen, bool terminal) => engineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => new StateChip("PARKED", ChipTone.Attn),
        EngineOwnedKind.Escalated => new StateChip("ESCALATED", ChipTone.Attn),
        EngineOwnedKind.TakenOver => new StateChip("WITH YOU", ChipTone.Neutral),
        _ when deskOpen => new StateChip("DECISION", ChipTone.Attn),
        _ when string.Equals(state, "queued", StringComparison.Ordinal) => new StateChip("QUEUED", ChipTone.Neutral),
        _ when terminal => new StateChip("DONE", ChipTone.Neutral),
        _ => new StateChip("RUNNING", ChipTone.Ok),
    };
}
