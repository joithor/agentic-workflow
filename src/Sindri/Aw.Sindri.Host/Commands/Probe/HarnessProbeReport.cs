namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// What 'sindri doctor --probe' records inside a harness (engine spec §6.2, P2 unit (e)): the agent-candidate variables
/// that are set (names only, never values), the whole process ancestry by name, and whether stdin and stdout are a
/// terminal. A golden test pins one report per harness; API spec §6's variable and ancestry lists are read from it.
/// </summary>
/// <param name="Harness">The harness the probe ran in, as the owner named it ('claude-code', 'codex:workspace-write', 'cursor-agent', 'cursor-terminal').</param>
/// <param name="Variables">The candidate variables set in this process, sorted.</param>
/// <param name="Ancestry">The ancestors' process names, nearest first.</param>
/// <param name="Terminal">Whether stdin and stdout are a terminal.</param>
/// <param name="Version">The harness's version, when it reports one.</param>
internal sealed record HarnessProbeReport(string Harness, IReadOnlyList<string> Variables, IReadOnlyList<string> Ancestry, bool Terminal, string? Version);
