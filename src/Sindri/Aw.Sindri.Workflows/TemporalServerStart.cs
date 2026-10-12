namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'sindri serve' passes to start the dev server.</summary>
/// <param name="Executable">The pinned 'temporal' CLI (binaries.lock).</param>
/// <param name="SindriHome">The Sindri home; the server's database is '&lt;home&gt;/temporal.db'.</param>
internal sealed record TemporalServerStart(string Executable, string SindriHome);
