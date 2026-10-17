namespace Aw.Sindri.Core.Sessions;

/// <summary>The outcome of one <see cref="FleetChangeRules.Diff"/>.</summary>
/// <param name="State">What to remember for the next pass.</param>
/// <param name="Changes">The changes to ledger.</param>
public sealed record FleetDiff(FleetTrackerState State, IReadOnlyList<FleetChange> Changes);
