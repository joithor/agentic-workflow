namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The parts of the status header line (E-23).</summary>
/// <param name="Address">The instance address.</param>
/// <param name="Recipe">The recipe id.</param>
/// <param name="Hash12">The first 12 hex digits of the recipe hash.</param>
/// <param name="Version">The instance version.</param>
/// <param name="State">The state shown.</param>
/// <param name="Detail">What follows the state, when there is one.</param>
/// <param name="Since">When the instance entered the state.</param>
/// <param name="AgeSeconds">Whole seconds since then.</param>
public sealed record HeaderParts(
    string Address,
    string Recipe,
    string Hash12,
    int Version,
    string State,
    string? Detail,
    DateTimeOffset Since,
    long AgeSeconds);
