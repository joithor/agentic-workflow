using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>'&lt;home&gt;/workflow/pr-watch.json' (PR babysitter spec §6.2): read whole, written whole, owner-only and atomically (a temporary file, flushed, then moved over it).</summary>
/// <param name="home">Where the file lives.</param>
internal sealed class PrWatchStore(WorkflowsHome home)
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default, indented: true);

    /// <summary>The file's content; an empty record when there is none.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The record, or the error of a file that can't be read or isn't one.</returns>
    public async ValueTask<Result<PrWatchFile>> Read(CancellationToken cancellationToken)
    {
        var file = home.PrWatchFile;
        byte[] bytes;
        try
        {
            if (!File.Exists(file))
            {
                return PrWatchFile.Empty;
            }

            bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable home is an expected failure
        {
            return Failed($"can't be read: {e.Message}");
        }

        var read = AwJson.Deserialize<PrWatchFile>(bytes, _json);
        return read.IsError ? Failed($"isn't the watcher's record: {read.Error.Message} (fix: delete it; the watcher records every pull request again and sends no event for what it saw before)") : read.Value;
    }

    /// <summary>Writes the file.</summary>
    /// <param name="content">What to record.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Nothing, or the error of a home that can't be written.</returns>
    public async ValueTask<Result<Unit>> Write(PrWatchFile content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var file = home.PrWatchFile;
        var directory = Path.GetDirectoryName(file)!; // why: the file is always under '<home>/workflow'
        var temporary = file + "." + Path.GetRandomFileName();
        try
        {
            _ = OperatingSystem.IsWindows() ? Directory.CreateDirectory(directory) : Directory.CreateDirectory(directory, OwnerDirectory);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFile;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(AwJson.Serialize(content, _json), cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);   // why: the move must not publish a name whose bytes a power cut could still lose
            }

            File.Move(temporary, file, overwrite: true);
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: a home that can't be written is an expected failure
        {
            return Failed($"can't be written: {e.Message} (fix: check the Sindri home's permissions and free space)");
        }
    }

    private Error Failed(string problem) => SindriErrors.ServiceFailed.With($"the PR watcher's record {home.PrWatchFile} {problem}");
}
