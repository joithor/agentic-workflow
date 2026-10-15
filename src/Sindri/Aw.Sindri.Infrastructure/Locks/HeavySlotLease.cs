namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// A held slot. Release deletes the owner file this process wrote, then the directory; twice is harmless. A directory whose owner
/// line names another process is left alone: someone removed ours by hand and another holder took the slot.
/// </summary>
/// <param name="directory">The slot's directory, or null for the inherited (nothing held on disk) lease.</param>
internal sealed class HeavySlotLease(string? directory) : IAsyncDisposable
{
    private int _released;

    /// <summary>A lease that holds nothing on disk: a parent already holds a slot.</summary>
    internal static HeavySlotLease Inherited { get; } = new(directory: null);

    public async ValueTask DisposeAsync()
    {
        if (directory is null || Interlocked.Exchange(ref _released, 1) == 1)
        {
            return;
        }

        try
        {
            var owner = Path.Combine(directory, DirectoryHeavySlots.OwnerFile);
            if (File.Exists(owner) && !DirectoryHeavySlots.IsOurs(await File.ReadAllTextAsync(owner).ConfigureAwait(false)))
            {
                return;
            }

            File.Delete(owner);
            Directory.Delete(directory, recursive: false);
        }
        catch (IOException)
        {
            // Something else is inside the directory; leave it, as the shell's release does (rmdir || true).
        }
        catch (UnauthorizedAccessException)
        {
            // Not ours to remove.
        }
    }
}
