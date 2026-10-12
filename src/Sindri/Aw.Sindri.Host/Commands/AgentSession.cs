using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// How the CLI classes itself (API spec §6): human only at an interactive terminal with no agent signal. An agent
/// signal is one of the agent-session variables, or an agent harness among the process's ancestors.
/// </summary>
/// <param name="Signal">The agent signal seen ('CLAUDECODE is set', 'parent process claude'), or null.</param>
/// <param name="Terminal">Whether stdin and stdout are a terminal.</param>
/// <param name="Prover">Whether AW_SINDRI_PROVER=1 (sindri-prove.sh human) is set.</param>
internal sealed record AgentSession(string? Signal, bool Terminal, bool Prover)
{
    /// <summary>Whether an agent signal was seen: such a CLI never starts the control plane (API spec §7).</summary>
    internal bool InAgent => Signal is not null;

    /// <summary>Whether the CLI calls as a human.</summary>
    internal bool Human => Terminal && Signal is null;

    /// <summary>Why the CLI calls as a session ('CLAUDECODE is set', 'parent process claude', 'no terminal'), or null for a human.</summary>
    internal string? SessionReason => Signal ?? (Terminal ? null : "no terminal");

    /// <summary>Classes this CLI.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="ancestry">This process's ancestry.</param>
    /// <param name="interactive">Whether stdin and stdout are a terminal.</param>
    /// <returns>The classification.</returns>
    internal static AgentSession Detect(Func<string, string?> environment, IProcessAncestry ancestry, bool interactive) =>
        throw new NotSupportedException("red: the agent variables, then the ancestry");
}
