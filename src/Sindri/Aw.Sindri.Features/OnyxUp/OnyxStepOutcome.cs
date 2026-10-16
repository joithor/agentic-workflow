namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What a step of `onyx up` did in one run.</summary>
public enum OnyxStepOutcome
{
    /// <summary>The step did work: its inputs changed, its result was gone or had drifted, or it had never run.</summary>
    Ran,

    /// <summary>The step's inputs and results were already as needed.</summary>
    Skipped,

    /// <summary>The step needs the human before it can go on (reserved: P3's account setup).</summary>
    NeedsHuman,
}
