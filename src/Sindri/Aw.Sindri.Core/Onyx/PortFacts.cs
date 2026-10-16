namespace Aw.Sindri.Core.Onyx;

/// <summary>Which loopback ports Onyx's web port could use.</summary>
/// <param name="Preferred">The port Onyx wants.</param>
/// <param name="PreferredFree">It could be bound on 127.0.0.1.</param>
/// <param name="FirstFreeFallback">The first free port of the fallback range, or null when every one is taken.</param>
public sealed record PortFacts(int Preferred, bool PreferredFree, int? FirstFreeFallback);
