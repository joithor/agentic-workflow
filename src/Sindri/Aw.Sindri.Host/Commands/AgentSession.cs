namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What the CLI knows about where it runs (API spec §6). Who it calls as is not decided here: a readable per-start token
/// makes it a human caller, whatever the terminal or the agent around it (<see cref="CallerHeaders"/>).
/// </summary>
/// <param name="Terminal">Whether stdin and stdout are a terminal.</param>
/// <param name="Prover">Whether AW_SINDRI_PROVER=1 (sindri-prove.sh human) is set: the CLI labels itself 'prover' for the proof home.</param>
internal sealed record AgentSession(bool Terminal, bool Prover)
{
    /// <summary>Set to 1 by scripts/sindri-prove.sh human: labels the CLI's calls 'prover'.</summary>
    internal const string ProverVariable = "AW_SINDRI_PROVER";

    /// <summary>Whether a person can answer here: the verbs that open a browser or ask a question need a terminal.</summary>
    internal bool Human => Terminal;

    /// <summary>Classes this CLI.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="interactive">Whether stdin and stdout are a terminal.</param>
    /// <returns>The classification.</returns>
    internal static AgentSession Detect(Func<string, string?> environment, bool interactive) =>
        new(interactive, string.Equals(environment(ProverVariable), "1", StringComparison.Ordinal));
}
