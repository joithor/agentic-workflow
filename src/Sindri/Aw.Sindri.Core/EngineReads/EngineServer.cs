namespace Aw.Sindri.Core.EngineReads;

/// <summary>The Temporal server's facts (API spec §3.8, E-30).</summary>
/// <param name="Kind">'temporal-dev', the dev server 'sindri serve' keeps.</param>
/// <param name="Namespace">The Temporal namespace Sindri's instances run in.</param>
/// <param name="UpSeconds">How long the server process has run, in seconds; 0 when it can't be told.</param>
/// <param name="DbBytes">The size of the server's database file, in bytes; 0 when there is none.</param>
/// <param name="UiUrl">The dev server's Web UI on loopback while it is up; null otherwise.</param>
public sealed record EngineServer(string Kind, string Namespace, long UpSeconds, long DbBytes, string? UiUrl);
