namespace Aw.Sindri.Core.Onyx;

/// <summary>What `sindri onyx up` has done, written to onyx/state.json after each step succeeds.</summary>
/// <param name="Port">The loopback port setup chose and keeps across reruns.</param>
/// <param name="Steps">Each completed step (see <see cref="OnyxSteps"/>) with the stamp of the inputs it ran with; a step whose stamp still matches is skipped.</param>
/// <param name="Project">The compose project name `onyx up` started the stack under; status, doctor and down act on it (null in a state written before it was recorded: the home's own name).</param>
/// <param name="SchemaVersion">The layout of this file (1); a file of another version is read as unreadable, never half-understood.</param>
public sealed record OnyxSetupState(int Port, IReadOnlyDictionary<string, string> Steps, string? Project = null, int SchemaVersion = OnyxSetupState.CurrentSchema)
{
    /// <summary>The schema version this build writes and reads.</summary>
    public const int CurrentSchema = 1;
}
