namespace Aw.Sindri.Core.Control;

/// <summary>What the doctor's 'temporal' check judges: the dev server on this home's database, if one runs, and how it answers.</summary>
/// <param name="Running">The server found on this home's database, or null when none runs.</param>
/// <param name="Pinned">The 'temporal' CLI version binaries.lock pins.</param>
/// <param name="Version">The running server's CLI version, or null when it can't be read.</param>
/// <param name="Reachable">Its frontend answers on loopback.</param>
/// <param name="NamespacePresent">The 'sindri' namespace exists on it.</param>
/// <param name="Problem">Why it didn't answer, when it didn't.</param>
public sealed record TemporalFacts(TemporalEndpoint? Running, string Pinned, string? Version, bool Reachable, bool NamespacePresent, string? Problem);
