using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IHostResources over /proc/meminfo, the state disk and the heavy-job lock directories. A lock slot is a directory
/// (the lock path, then .2, .3 ...) whose owner file starts with the holder's pid; a slot with no readable owner is
/// listed with no pid.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
/// <param name="heavyLock">The heavy-job lock path (slot 1); the other slots are its siblings.</param>
internal sealed class ProcHostResources(string stateDirectory, string heavyLock) : IHostResources
{
    public async ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken)
    {
        try
        {
            var meminfo = ProcFormat.MemInfo(await File.ReadAllTextAsync("/proc/meminfo", cancellationToken).ConfigureAwait(false));
            return new HostResources(meminfo.Total, meminfo.Available, meminfo.SwapUsed, DiskSpace.Free(stateDirectory), meminfo.SwapTotal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return MetaErrors.HostUnreadable.With(exception.Message);
        }
    }

    public async ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken)
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
            taken.Add(await Holder(slot, cancellationToken).ConfigureAwait(false));
        }

        return taken;
    }

    private static bool IsSlot(string directory, string name) =>
        directory == name
        || (directory.StartsWith(name + ".", StringComparison.Ordinal) && int.TryParse(directory[(name.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _));

    private static async ValueTask<HeavyLock> Holder(string slot, CancellationToken cancellationToken)
    {
        try
        {
            var text = (await File.ReadAllTextAsync(Path.Combine(slot, "owner"), cancellationToken).ConfigureAwait(false)).Trim();
            var first = text.Split(' ', 2)[0];
            return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                ? new HeavyLock(slot, pid, Directory.Exists("/proc/" + first), text)
                : new HeavyLock(slot, Pid: null, Alive: false, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new HeavyLock(slot, Pid: null, Alive: false, Holder: string.Empty);
        }
    }
}
