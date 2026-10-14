using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands.Probe;

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
    /// <summary>Set to 1 by scripts/sindri-prove.sh human: skips the parent-process check, never the variables.</summary>
    internal const string ProverVariable = "AW_SINDRI_PROVER";

    /// <summary>How many ancestors the parent-process check reads.</summary>
    private const int Depth = 32;

    /// <summary>Set by Sindri on the processes it starts (its recursion guard): Sindri's own marker, not a probed harness's.</summary>
    internal const string SindriChild = "AW_SINDRI_CHILD";

    /// <summary>
    /// The agent-session variables (API spec §6): the pinned harness probe's markers (engine spec §6.2,
    /// <see cref="PinnedProbe"/>), then Sindri's own.
    /// </summary>
    internal static readonly IReadOnlyList<string> Variables = [.. PinnedProbe.Variables, SindriChild];

    /// <summary>The ancestry patterns of the agent harnesses, from the pinned probe.</summary>
    internal static readonly IReadOnlyList<HarnessProcess> Harnesses = PinnedProbe.Processes;

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
    internal static AgentSession Detect(Func<string, string?> environment, IProcessAncestry ancestry, bool interactive)
    {
        var prover = string.Equals(environment(ProverVariable), "1", StringComparison.Ordinal);
        return new AgentSession(Variable(environment) ?? (prover ? null : Harness(ancestry)), interactive, prover);
    }

    private static string? Variable(Func<string, string?> environment) =>
        Variables.FirstOrDefault(v => environment(v) is { Length: > 0 }) is { } set ? $"{set} is set" : null;

    /// <summary>The first ancestor that is an agent harness, named as the refusal shows it (engine spec §6.2).</summary>
    private static string? Harness(IProcessAncestry ancestry)
    {
        foreach (var ancestor in ancestry.Ancestors(Environment.ProcessId, Depth))
        {
            var name = HarnessProcess.BaseName(ancestor.Name);
            if (Harnesses.Any(h => h.Name is not null && h.Matches(ancestor.Name, executable: null)))
            {
                return $"parent process {name}";
            }

            if (Harnesses.Any(h => h.Matches(ancestor)))
            {
                return $"parent process {name} ({ancestor.Executable})";
            }
        }

        return null;
    }
}
