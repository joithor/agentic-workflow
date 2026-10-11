namespace Aw.Evidence.Testing;

/// <summary>The local stack a <see cref="StackProbeContract"/> test arranges.</summary>
/// <param name="LoginTitle">The login page's &lt;title&gt;, or null for a page without one.</param>
/// <param name="DatabaseReady">Whether the database accepts connections.</param>
/// <param name="PortOpen">Whether something listens on the probed port.</param>
public sealed record StackState(string? LoginTitle, bool DatabaseReady, bool PortOpen);
