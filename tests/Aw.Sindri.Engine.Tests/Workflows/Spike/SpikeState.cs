namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>A state as the test interpreter needs it.</summary>
/// <param name="Name">The state's name.</param>
/// <param name="Terminal">True for a terminal state.</param>
/// <param name="Timeout">An active state's timeout.</param>
/// <param name="Loop">The loop the state belongs to.</param>
internal sealed record SpikeState(string Name, bool Terminal, TimeSpan Timeout, string? Loop);
