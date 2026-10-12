namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// What 'sindri doctor --probe' records inside a harness (engine spec §6.2, P2 unit (e)): the names of every environment
/// variable set (never a value), the whole process ancestry by name and executable path up to launchd or init, whether
/// stdin and stdout are a terminal, and the harness's version when one can be read. A golden test pins one report per
/// harness (tests/Aw.Sindri.Tests/Golden/Probe/); API spec §6's variable and ancestry lists are read from it
/// (<see cref="PinnedProbe"/>).
/// </summary>
/// <param name="Harness">The harness the probe ran in, as the owner named it ('claude-code', 'codex:workspace-write', 'cursor-agent', 'cursor-terminal'), or the one the pinned probe recognised.</param>
/// <param name="Variables">The names of the variables set in this process, sorted ordinally.</param>
/// <param name="Ancestry">The ancestors, nearest first, ending at pid 1.</param>
/// <param name="Terminal">Whether stdin and stdout are a terminal.</param>
/// <param name="Version">The harness's version, when it reports one.</param>
internal sealed record HarnessProbeReport(string Harness, IReadOnlyList<string> Variables, IReadOnlyList<ProbedProcess> Ancestry, bool Terminal, string? Version);
