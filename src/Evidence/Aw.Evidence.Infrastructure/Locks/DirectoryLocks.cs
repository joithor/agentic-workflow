using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>
/// Machine locks as directories, interoperable with config/lib/locks.sh and the heavy wrapper: a lock is held while its
/// directory exists, taken with an atomic mkdir and polled every second. The holder writes '&lt;pid&gt; &lt;utc&gt;
/// ui-evidence' to &lt;dir&gt;/owner. With AW_HEAVY_LOCK_HELD=1 a parent already holds the heavy-job lock, so taking it
/// is a no-op. There is no stale-lock takeover: a crashed holder's directory is removed by a person.
/// </summary>
internal sealed class DirectoryLocks(LockSettings settings, TimeProvider clock, TextWriter notices) : IMachineLocks
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    public ValueTask<Result<ILockLease>> AcquireAsync(LockKind kind, TimeSpan timeout, CancellationToken cancellationToken) =>
        kind == LockKind.HeavyJob && settings.HeavyJobHeld
            ? ValueTask.FromResult<Result<ILockLease>>(DirectoryLease.Inherited(kind))
            : WaitAsync(kind, kind == LockKind.QaStack ? settings.QaStack : settings.HeavyJob, timeout, cancellationToken);

    private async ValueTask<Result<ILockLease>> WaitAsync(LockKind kind, string directory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + timeout;
        var announced = false;
        while (true)
        {
            var taken = TryTake(directory);
            if (taken.IsError || taken.Value)
            {
                return taken.IsError ? taken.Error : Hold(kind, directory);
            }

            var left = deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return EvidenceErrors.LockBusy.With(string.Create(CultureInfo.InvariantCulture, $"{directory} stayed held for {timeout.TotalSeconds:0.#} s"));
            }

            if (!announced)
            {
                await notices.WriteLineAsync($"ui-evidence: waiting on lock: {directory}".AsMemory(), cancellationToken).ConfigureAwait(false);
                announced = true;
            }

            await Task.Delay(left < _poll ? left : _poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Result<bool> TryTake(string directory)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(directory) ?? directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return EvidenceErrors.FileUnavailable.With($"can't create the lock directory's parent for {directory}: {ex.Message}");
        }

        var errno = Libc.MakeDirectory(directory);
        return errno switch
        {
            0 => true,
            Libc.AlreadyExists => false,
            _ => EvidenceErrors.FileUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"mkdir {directory} failed (errno {errno})")),
        };
    }

    private DirectoryLease Hold(LockKind kind, string directory)
    {
        var owner = string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {clock.GetUtcNow().UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} ui-evidence\n");
        try
        {
            File.WriteAllText(Path.Combine(directory, OwnerFile), owner);
        }
        catch (IOException)
        {
            // The owner line is for people reading a stuck lock; the lock itself is the directory, already held.
        }

        return new DirectoryLease(kind, directory);
    }
}
