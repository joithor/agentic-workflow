namespace Aw.Evidence.Core.Steps;

/// <summary>How a step runs.</summary>
/// <param name="ActionTimeout">How long an action waits for its element.</param>
/// <param name="ExpectationTimeout">How long the expected state may take to hold.</param>
/// <param name="AllowRepair">Whether a broken click or fill may ask the judge for a replacement.</param>
public sealed record StepRunOptions(TimeSpan ActionTimeout, TimeSpan ExpectationTimeout, bool AllowRepair)
{
    /// <summary>A scripted run: 5 s actions, 3 s expectations, repair allowed.</summary>
    public static StepRunOptions Run { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3), AllowRepair: true);

    /// <summary>A parity frame's steps: no repair (a design state is precise or it isn't).</summary>
    /// <param name="actionTimeout">The action timeout.</param>
    /// <returns>The options.</returns>
    public static StepRunOptions Parity(TimeSpan actionTimeout) => new(actionTimeout, TimeSpan.FromSeconds(3), AllowRepair: false);
}
