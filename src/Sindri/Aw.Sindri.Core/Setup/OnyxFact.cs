using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Core.Setup;

/// <summary>Where the managed Onyx stands, from its status.</summary>
/// <param name="State">Not set up, running, stopped or unknown.</param>
/// <param name="Unreachable">True when it runs but doesn't answer.</param>
/// <param name="BlockedByCode">The error code of the first failed preflight check (Docker, Compose, memory, disk or port), or null when every check passes.</param>
public sealed record OnyxFact(OnyxState State, bool Unreachable, string? BlockedByCode);
