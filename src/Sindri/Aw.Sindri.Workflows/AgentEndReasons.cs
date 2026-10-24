using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Workflows;

/// <summary>The reason a Step fails with when its agent's session ended without a report (the word the failed Step, the ledger and the app carry).</summary>
internal static class AgentEndReasons
{
    /// <summary>The reason for how the session ended.</summary>
    /// <param name="ended">How it ended; null reads as an exit.</param>
    /// <returns>'lost', 'idle' or 'exit'.</returns>
    internal static string Of(AgentSessionEnd? ended) => ended == AgentSessionEnd.Lost ? "lost" : "exit";
}
