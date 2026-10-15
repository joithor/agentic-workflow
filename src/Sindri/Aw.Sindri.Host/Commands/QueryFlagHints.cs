using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>Adds the CLI's own flag to the transport-neutral 006 a depth or budget gets from the engine (the engine's message names no flag, so HTTP and MCP callers aren't told to use one).</summary>
internal static class QueryFlagHints
{
    internal static Error Add(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Code != ContextErrors.QueryInvalid.Code)
        {
            return error;
        }

        return error.Message switch
        {
            var m when m.StartsWith("depth must be", StringComparison.Ordinal) => error with { Message = m + " (fix: --depth 2)" },
            var m when m.StartsWith("budget must be", StringComparison.Ordinal) => error with { Message = m + " (fix: --budget 4000)" },
            _ => error,
        };
    }
}
