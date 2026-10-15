using System.Text;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IRunningLog over a markdown file. .NET opens FileMode.Append without O_APPEND and tracks the end of file itself, so
/// two writers could overwrite each other; the file is therefore opened exclusively (FileShare.None is an flock on Unix)
/// for the one write of each line, and a writer that finds it taken waits its turn.
/// </summary>
/// <param name="path">Resolves the log file's path at each append (the team config can change it).</param>
internal sealed class AppendRunningLog(Func<CancellationToken, ValueTask<string>> path) : IRunningLog
{
    private const int Attempts = 200;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(10);

    public async ValueTask<Result<Unit>> Append(string line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        var file = await path(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(file)) is { } directory)
            {
                _ = Directory.CreateDirectory(directory);
            }

            var stream = await OpenExclusive(file, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), cancellationToken).ConfigureAwait(false);
            }

            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.LogUnwritable.With($"{file}: {exception.Message}");
        }
    }

    private static async ValueTask<FileStream> OpenExclusive(string file, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 1, useAsync: true);
            }
            catch (IOException exception) when (exception.GetType() == typeof(IOException) && attempt < Attempts) // the sharing violation; more specific I/O errors are not retried
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
