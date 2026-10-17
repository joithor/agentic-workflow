using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// The heavy-job lock directories, the same on every OS. A lock slot is a directory (the lock path, then .2, .3 ...) whose
/// owner file starts with the holder's pid; a slot with no readable owner is listed with no pid.
/// </summary>
internal static class HeavyLockDirectory
{
    internal static async ValueTask<Result<IReadOnlyList<HeavyLock>>> List(string heavyLock, IProcessLiveness liveness, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(heavyLock));
        if (parent is null || !Directory.Exists(parent))
        {
            return Results.Success<IReadOnlyList<HeavyLock>>([]);
        }

        var name = Path.GetFileName(heavyLock);
        var taken = new List<HeavyLock>();
        foreach (var slot in Directory.EnumerateDirectories(parent).Where(d => IsSlot(Path.GetFileName(d), name)).Order(StringComparer.Ordinal))
        {
            var holder = await Holder(slot, liveness, cancellationToken).ConfigureAwait(false);
            if (holder.IsError)
            {
                return holder.Error;
            }

            taken.Add(holder.Value);
        }

        return taken;
    }

    private static bool IsSlot(string directory, string name) =>
        directory == name
        || (directory.StartsWith(name + ".", StringComparison.Ordinal) && int.TryParse(directory[(name.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _));

    private static async ValueTask<Result<HeavyLock>> Holder(string slot, IProcessLiveness liveness, CancellationToken cancellationToken)
    {
        try
        {
            var owner = Path.Combine(slot, "owner");
            var text = (await File.ReadAllTextAsync(owner, cancellationToken).ConfigureAwait(false)).Trim();
            var first = text.Split(' ', 2)[0];
            if (!int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                return new HeavyLock(slot, Pid: null, Alive: false, text);
            }

            // the owner file's time is when the lock was taken: a process that started after it is not the holder (the pid was reused)
            var alive = await liveness.IsRunning(pid, new DateTimeOffset(File.GetLastWriteTimeUtc(owner), TimeSpan.Zero), cancellationToken).ConfigureAwait(false);
            return alive.IsError ? alive.Error : new HeavyLock(slot, pid, alive.Value, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HeavyLock(slot, Pid: null, Alive: false, Holder: string.Empty);
        }
    }
}
