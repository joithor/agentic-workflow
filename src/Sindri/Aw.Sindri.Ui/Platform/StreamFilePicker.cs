namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// <see cref="IFilePicker"/> over a platform chooser: the chooser says which file, this reads it and applies the size limit, so the limit is one
/// piece of code every head shares and tests can reach without a window. A file larger than the limit is reported by its size and left unread
/// (read no further than one byte past the limit when the platform does not know its size); no choice, or a refusal, is null.
/// </summary>
/// <param name="choose">The platform's chooser: the picker's title in, the chosen file or null out.</param>
internal sealed class StreamFilePicker(Func<string, CancellationToken, Task<ChosenFile?>> choose) : IFilePicker
{
    public async Task<PickedFile?> Pick(string title, int limit, CancellationToken cancellationToken)
    {
        try
        {
            if (await choose(title, cancellationToken).ConfigureAwait(true) is not { } file)
            {
                return null;
            }

            using (file.Owner)   // the platform's hold on the file ends with the read, whatever its size
            {
                return await Read(file, limit, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return null;   // the platform's refusal is an answer, not a fault
        }
    }

    private static async Task<PickedFile> Read(ChosenFile file, int limit, CancellationToken cancellationToken)
    {
        if (file.Size is { } known && known > limit)
        {
            return new PickedFile(file.Name, known, ReadOnlyMemory<byte>.Empty);
        }

        // Sized from the reported size when there is one (one byte more, so a size the platform under-reports still stops one byte past the limit).
        var buffer = new byte[file.Size is { } reported ? (int)Math.Min(reported + 1, (long)limit + 1) : limit + 1];
        var read = 0;
        var stream = await file.Open(cancellationToken).ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            while (read <= limit)
            {
                if (read == buffer.Length)
                {
                    Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, (long)limit + 1));   // a platform that under-reports its size still gets read up to one byte past the limit
                }

                if (await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(true) is not (var n and > 0))
                {
                    break;
                }

                read += n;
            }
        }

        return read > limit ? new PickedFile(file.Name, read, ReadOnlyMemory<byte>.Empty) : new PickedFile(file.Name, read, buffer.AsMemory(0, read));
    }
}
