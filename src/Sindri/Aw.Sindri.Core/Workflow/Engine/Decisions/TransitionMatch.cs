namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// Which recipe transition a trigger selects (spec §3.5): the first, in file order, from the current state on the
/// trigger whose 'when' holds. Known before the gates run, so the caller evaluates exactly that transition's gates.
/// </summary>
/// <param name="Transition">The index of the matched transition in the recipe's transitions; null on a miss.</param>
/// <param name="Miss">Why nothing matched; <see cref="MatchMiss.None"/> on a match.</param>
public sealed record TransitionMatch(int? Transition, MatchMiss Miss);
