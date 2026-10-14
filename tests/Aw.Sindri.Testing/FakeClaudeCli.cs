using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Testing;

/// <summary>The claude CLI in memory: records every run and answers with the set result. Kept honest by <see cref="ClaudeCliContract"/>.</summary>
public sealed class FakeClaudeCli : IClaudeCli
{
    private readonly List<(IReadOnlyList<string> Arguments, string Directory)> _runs = [];

    /// <summary>What each run answers; null means claude couldn't be started.</summary>
    public ClaudeRun? Answer { get; set; } = new(0, string.Empty);

    /// <summary>Directories that don't exist: a run there answers null, as the process adapter does.</summary>
    public ISet<string> MissingDirectories { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Every run so far: its arguments and working directory.</summary>
    public IReadOnlyList<(IReadOnlyList<string> Arguments, string Directory)> Runs => _runs;

    /// <inheritdoc/>
    public ValueTask<ClaudeRun?> Run(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        if (MissingDirectories.Contains(workingDirectory))
        {
            return ValueTask.FromResult<ClaudeRun?>(null);
        }

        if (Answer is not null)
        {
            _runs.Add((arguments, workingDirectory));
        }

        return ValueTask.FromResult(Answer);
    }
}
