namespace Aw.Context.Infrastructure.Linear;

/// <summary>The data of the Probe operation (only that the three reads answered).</summary>
/// <param name="Projects">The Projects Linear serves.</param>
/// <param name="Documents">The Documents Linear serves.</param>
/// <param name="Issues">The Issues Linear serves.</param>
internal sealed record LinearProbeData(LinearConnection<LinearNamed>? Projects = null, LinearConnection<LinearNamed>? Documents = null, LinearConnection<LinearNamed>? Issues = null);
