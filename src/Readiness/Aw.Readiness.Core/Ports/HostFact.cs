namespace Aw.Readiness.Core.Ports;

/// <summary>A yes/no fact the repo host reports, or unknown.</summary>
/// <param name="Holds">True or false, or null when it couldn't be read.</param>
/// <param name="Evidence">What the host answered, or why it couldn't be read.</param>
public sealed record HostFact(bool? Holds, string Evidence);
