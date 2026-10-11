using Aw.Evidence.Core.Locks;

namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>A held lock directory. Release deletes the owner file this process wrote, then the directory; twice is harmless.</summary>
internal sealed class DirectoryLease(LockKind kind, string? directory) : ILockLease
{
    private int _released;

    /// <summary>A lease that holds nothing on disk: the lock is already held by a parent process.</summary>
    /// <param name="kind">The lock.</param>
    /// <returns>The lease.</returns>
    internal static DirectoryLease Inherited(LockKind kind) => new(kind, directory: null);

    /// <inheritdoc/>
    public IReadOnlyList<LockKind> Held { get; } = [kind];

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (directory is null || Interlocked.Exchange(ref _released, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            File.Delete(Path.Combine(directory, DirectoryLocks.OwnerFile));
            Directory.Delete(directory, recursive: false);
        }
        catch (IOException)
        {
            // Something else is inside the directory; leave it, as the shell's release does (rmdir || true).
        }
        catch (UnauthorizedAccessException)
        {
            // Not ours to remove; same as above.
        }

        return ValueTask.CompletedTask;
    }
}
