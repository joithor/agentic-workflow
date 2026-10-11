using Aw.Evidence.Core.Browser;

namespace Aw.Evidence.Core.Judging;

/// <summary>A broken selector and the visible elements that could replace it.</summary>
/// <param name="BrokenSelector">The step's target that no longer resolves.</param>
/// <param name="Step">The step and its expectation, described.</param>
/// <param name="Candidates">The currently visible interactive elements.</param>
public sealed record SelectorRepairRequest(string BrokenSelector, string Step, IReadOnlyList<Candidate> Candidates);
