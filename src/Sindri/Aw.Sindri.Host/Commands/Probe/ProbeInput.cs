using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>What the probe reads from this process.</summary>
/// <param name="Harness">The harness name the owner gave, or null to name the one the pinned probe recognises.</param>
/// <param name="Variables">The names of the variables set (never their values).</param>
/// <param name="Home">The home directory, written '~' in executable paths; null leaves paths as they are.</param>
/// <param name="Ancestry">This process's ancestors, nearest first.</param>
/// <param name="Terminal">Whether stdin and stdout are a terminal.</param>
internal sealed record ProbeInput(string? Harness, IReadOnlyCollection<string> Variables, string? Home, IReadOnlyList<ProcessAncestor> Ancestry, bool Terminal);
