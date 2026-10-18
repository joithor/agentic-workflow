namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>A transition as the test interpreter needs it: from, to, on and the '{ outcome }' form of 'when'.</summary>
/// <param name="From">The states it leaves from.</param>
/// <param name="To">The state it enters.</param>
/// <param name="On">The trigger.</param>
/// <param name="Outcome">The outcome its 'when' names, if any.</param>
internal sealed record SpikeTransition(IReadOnlyList<string> From, string To, string On, string? Outcome);
