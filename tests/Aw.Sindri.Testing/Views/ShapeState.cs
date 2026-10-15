#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record

namespace Aw.Sindri.Testing.Views;

/// <summary>One state of a fixture recipe: where it sits in the path order (spec 3.10) and what it is.</summary>
/// <param name="Id">The state id.</param>
/// <param name="Order">1-based path order, null for a branch state.</param>
/// <param name="BranchOf">The state a branch folds under.</param>
/// <param name="Terminal">A final state.</param>
/// <param name="Desk">Its step is a desk step.</param>
/// <param name="Child">It waits on a child recipe.</param>
public sealed record ShapeState(string Id, int? Order, string? BranchOf = null, bool Terminal = false, bool Desk = false, bool Child = false);
