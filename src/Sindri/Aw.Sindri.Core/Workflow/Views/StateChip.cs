using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The one state chip of the screens fixture ("State chips: one mapping of word to colour"): a word and the colour class it takes.</summary>
/// <param name="Word">The chip word, upper case.</param>
/// <param name="Tone">The colour class.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateChip(string Word, ChipTone Tone)
{
    /// <summary>An instance parked: it waits for an answer.</summary>
    public const string Parked = "PARKED";

    /// <summary>An instance escalated: a human decides.</summary>
    public const string Escalated = "ESCALATED";

    /// <summary>An instance a person took over.</summary>
    public const string WithYou = "WITH YOU";

    /// <summary>An instance whose desk answer is open.</summary>
    public const string Decision = "DECISION";

    /// <summary>An instance that has not started.</summary>
    public const string Queued = "QUEUED";

    /// <summary>An instance that is finished.</summary>
    public const string Done = "DONE";

    /// <summary>An instance that is running.</summary>
    public const string Running = "RUNNING";

    /// <summary>The chip of an instance: an engine-owned badge decides first, then an open desk answer, then not started, terminal and running.</summary>
    /// <param name="engineOwned">The engine-owned badge, if any; a taken-over instance arrives here as the "taken-over" badge (the plan's "takenOver" flag).</param>
    /// <param name="deskOpen">Whether a desk answer is open.</param>
    /// <param name="terminal">Whether the state is final.</param>
    /// <param name="notStarted">Whether the instance hasn't started: it sits at its recipe's first state, waiting for a slot.</param>
    /// <returns>The chip.</returns>
    public static StateChip For(EngineOwnedBadge? engineOwned, bool deskOpen, bool terminal, bool notStarted = false) => engineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => new StateChip(Parked, ChipTone.Attn),
        EngineOwnedKind.Escalated => new StateChip(Escalated, ChipTone.Attn),
        EngineOwnedKind.TakenOver => new StateChip(WithYou, ChipTone.Neutral),
        _ when deskOpen => new StateChip(Decision, ChipTone.Attn),
        _ when notStarted => new StateChip(Queued, ChipTone.Neutral),
        _ when terminal => new StateChip(Done, ChipTone.Neutral),
        _ => new StateChip(Running, ChipTone.Ok),
    };
}
