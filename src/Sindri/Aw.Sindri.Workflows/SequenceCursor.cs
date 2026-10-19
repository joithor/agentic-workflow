using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A background service's cursor (engine spec rev 8 §5.4; engine P4 unit (d)): the last ledger sequence it has handled, as
/// one owner-only (0600) file under the Sindri home. A missing file reads as 0, the start of the ledger. A write goes to a
/// temporary name and is moved into place, so a crash never leaves half a number; a crash between a delivery and the write
/// repeats that delivery, which the receiver drops by its ledger sequence or idempotency key. The event bridge's and the
/// router stub's cursors are the two files.
/// </summary>
/// <param name="file">The cursor file.</param>
/// <param name="name">Whose cursor it is, for the failure text ("the event bridge's cursor").</param>
internal abstract class SequenceCursor(string file, string name)
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;

    /// <summary>The last delivered sequence.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sequence, 0 when nothing was delivered yet, or SND-SCHED-001 when the file can't be read or holds no sequence.</returns>
    public async ValueTask<Result<long>> Read(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            if (!File.Exists(file))
            {
                return 0L;
            }

            text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable home is an expected failure
        {
            return Failed($"can't be read: {e.Message}");
        }

        return long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            ? sequence
            : Failed($"holds '{text.Trim()}', not a ledger sequence (fix: delete it; the service starts from the beginning of the ledger and what it had already done is dropped as a repeat)");
    }

    /// <summary>Records <paramref name="sequence"/> as delivered.</summary>
    /// <param name="sequence">The last delivered sequence.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done, or SND-SCHED-001 when the file can't be written.</returns>
    public async ValueTask<Result<Unit>> Advance(long sequence, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(file)!; // why: the cursor file is always under '<home>/workflow'
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
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "\n"), cancellationToken).ConfigureAwait(false);
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

    private Error Failed(string problem) => SindriErrors.ServiceFailed.With($"{name} {file} {problem}");
}
