namespace Aw.Sindri.Core.Mcp;

/// <summary>The claude CLI, run with an argument list (never a shell line) in a directory ('sindri mcp register --apply').</summary>
public interface IClaudeCli
{
    /// <summary>Runs claude with <paramref name="arguments"/> in <paramref name="workingDirectory"/>.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="workingDirectory">Where it runs.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>How it ended, or null when it couldn't be started (not installed, not executable, or the directory doesn't exist).</returns>
    public ValueTask<ClaudeRun?> Run(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken);
}
