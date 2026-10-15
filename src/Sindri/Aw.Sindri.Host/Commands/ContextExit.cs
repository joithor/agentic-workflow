namespace Aw.Sindri.Host.Commands;

/// <summary>The exit codes of the context verbs (context spec §8.6): 0 ok, 1 attention, 2 a tool error, 75 the heavy-job lock stayed busy.</summary>
internal static class ContextExit
{
    /// <summary>The exit code for <paramref name="error"/>.</summary>
    /// <param name="error">The error the API answered.</param>
    /// <param name="toolExit">What the Output wrote for it (1 for 'not running', else 2).</param>
    /// <returns>The code.</returns>
    internal static int For(Kernel.Error error, int toolExit) => error is null ? 0 : toolExit + 100;
}
