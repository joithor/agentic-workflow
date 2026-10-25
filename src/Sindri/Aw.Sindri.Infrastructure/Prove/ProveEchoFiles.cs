using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary>
/// The echo input's files, at fixed names under the prover home: 'echo.txt' holds the text it serves and 'echo.log' gets one line per call.
/// Nothing is read from the environment, so no variable names a path.
/// </summary>
/// <param name="directory">The folder holding both files.</param>
internal sealed class ProveEchoFiles(string directory) : IProveEcho
{
    /// <inheritdoc/>
    public async ValueTask<string> ReadText(CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "echo.txt");
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : throw new FileNotFoundException($"the echo input needs its text in {path} (fix: write the section text there)", path);
    }

    /// <inheritdoc/>
    public async ValueTask Log(string line, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(directory);
        await File.AppendAllTextAsync(Path.Combine(directory, "echo.log"), line + "\n", cancellationToken).ConfigureAwait(false);
    }
}
