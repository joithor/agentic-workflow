namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The run a state is on when an agent session runs its Step (engine P8): who runs it and the launch that carries it. Served
/// only while such a run exists, so every reader treats its absence as the ordinary case.
/// </summary>
/// <param name="Runtime">The runtime that runs the Step ('agent').</param>
/// <param name="Session">The session that runs the step, when the engine knows it; null otherwise.</param>
/// <param name="Launch">The launch id (LCH-…) of the session; opaque.</param>
internal sealed record StatusRun(string Runtime, string Launch, StatusSession? Session = null);
