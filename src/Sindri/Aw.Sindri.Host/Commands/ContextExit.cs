using Aw.Context.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>The exit codes of the context verbs (context spec §8.6): 0 ok, 1 attention, 2 a tool error, 75 the heavy-job lock stayed busy.</summary>
internal static class ContextExit
{
    private static readonly string[] _always = ["SND-API-001", "SND-AUTH-002"];

    /// <summary>The exit code for <paramref name="error"/>.</summary>
    /// <param name="error">The error the API answered.</param>
    /// <param name="toolExit">What the Output wrote for it (1 for 'not running', else 2).</param>
    /// <returns>The code.</returns>
    internal static int For(Kernel.Error error, int toolExit)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, ContextErrors.HeavyLockBusy.Code, StringComparison.Ordinal) ? 75
            : ContextErrors.Attention.Contains(error.Code) || _always.Contains(error.Code, StringComparer.Ordinal) ? 1
            : toolExit;
    }
}
