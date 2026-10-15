namespace Aw.Meta.Core.Watch;

/// <summary>The outcome of one poll.</summary>
/// <param name="Lines">One line per settled change.</param>
/// <param name="State">The state to carry to the next poll.</param>
public sealed record WatchStep(IReadOnlyList<string> Lines, WatchState State);
