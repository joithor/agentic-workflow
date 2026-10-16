namespace Aw.Sindri.Core.Onyx;

/// <summary>What `sindri onyx up` has done, written to onyx/state.json after each step succeeds.</summary>
/// <param name="Port">The loopback port setup chose and keeps across reruns.</param>
/// <param name="Steps">Each completed step (see <see cref="OnyxSteps"/>) with the stamp of the inputs it ran with; a step whose stamp still matches is skipped.</param>
public sealed record OnyxSetupState(int Port, IReadOnlyDictionary<string, string> Steps);
