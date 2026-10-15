using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>
/// The heavy-job lock as directories, interoperable with config/lib/locks.sh, the heavy wrapper and Evidence's directory locks: a
/// slot is held while its directory exists, taken with an atomic mkdir and polled every second. Slot 1 is the lock's path and slot k
/// is '&lt;path&gt;.k', tried in order, so a holder that knows only the path occupies slot 1 and the total never exceeds the slot count.
/// The holder writes '&lt;pid&gt; &lt;utc&gt; &lt;holder&gt;' to &lt;slot&gt;/owner. With AW_HEAVY_LOCK_HELD=1 a parent already holds a slot, so taking
/// one is a no-op. Like the heavy wrapper, a slot whose owner process is gone is reclaimed.
/// </summary>
/// <param name="settings">Where the lock is and how many slots it has.</param>
/// <param name="clock">The clock the wait runs on.</param>
internal sealed class DirectoryHeavySlots(HeavyLockSettings settings, TimeProvider clock) : IHeavySlots
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    public async ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);
        if (settings.Held)
        {
            return new HeavySlotTaken(HeavySlotLease.Inherited, string.Empty);
        }

        var slots = Slots();
        var deadline = clock.GetUtcNow() + timeout;
        var announced = false;
        while (true)
        {
            var taken = TryTakeAny(slots, holder);
            if (taken.IsError || taken.Value is not null)
            {
                return taken.IsError ? taken.Error : new HeavySlotTaken(taken.Value, string.Empty);
            }

            var left = deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return new HeavySlotTaken(Lease: null, Holders(slots));
            }

            if (!announced && waiting is not null)
            {
                waiting.Report($"waiting for heavy-job lock ({Holders(slots)})");
                announced = true;
            }

            await Task.Delay(left < _poll ? left : _poll, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Whether an owner line names this process.</summary>
    /// <param name="ownerLine">The owner file's text.</param>
    /// <returns>True when its first word is our pid.</returns>
    internal static bool IsOurs(string ownerLine) => ownerLine.Split(' ', 2)[0] == Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    private string[] Slots() =>
        [.. Enumerable.Range(1, settings.Slots).Select(k => k == 1 ? settings.Lock : string.Create(CultureInfo.InvariantCulture, $"{settings.Lock}.{k}"))];

    private Result<HeavySlotLease?> TryTakeAny(string[] slots, string holder)
    {
        foreach (var slot in slots)
        {
            var taken = TryTake(slot);
            if (taken.IsError)
            {
                return taken.Error;
            }

            if (taken.Value)
            {
                WriteOwner(slot, holder);
                return new HeavySlotLease(slot);
            }
        }

        return (HeavySlotLease?)null;
    }

    private static Result<bool> TryTake(string directory)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(directory) ?? directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ServiceFailed.With($"can't create the lock directory's parent for {directory}: {ex.Message}");
        }

        var errno = Libc.MakeDirectory(directory);
        if (errno == Libc.AlreadyExists && Reclaim(directory))
        {
            errno = Libc.MakeDirectory(directory);
        }

        return errno switch
        {
            0 => true,
            Libc.AlreadyExists => false,
            _ => SindriErrors.ServiceFailed.With(string.Create(CultureInfo.InvariantCulture, $"mkdir {directory} failed (errno {errno})")),
        };
    }

    /// <summary>Removes a slot whose owner process has died (the heavy wrapper does the same); false when it is held, has no owner line, or can't be removed.</summary>
    private static bool Reclaim(string directory)
    {
        try
        {
            var owner = Path.Combine(directory, OwnerFile);
            if (!File.Exists(owner) || !int.TryParse(File.ReadAllText(owner).Split(' ', 2)[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || Alive(pid))
            {
                return false;
            }

            File.Delete(owner);
            Directory.Delete(directory, recursive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void WriteOwner(string directory, string holder)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {clock.GetUtcNow().UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} {holder}\n");
        try
        {
            File.WriteAllText(Path.Combine(directory, OwnerFile), line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The owner line is for people reading a stuck lock; the lock itself is the directory, already held.
        }
    }

    /// <summary>'slot 1/2 held by 4242 sindri context-map-build, slot 2/2 held by 77 heavy …': what the waiter and the refusal say.</summary>
    private static string Holders(string[] slots) => string.Join(", ", slots.Select((slot, i) =>
    {
        var owner = Path.Combine(slot, OwnerFile);
        var by = File.Exists(owner) ? SafeRead(owner) : "an unknown holder";
        return string.Create(CultureInfo.InvariantCulture, $"slot {i + 1}/{slots.Length} held by {by}");
    }));

    private static string SafeRead(string owner)
    {
        try
        {
            var parts = File.ReadAllText(owner).Trim().Split(' ', 3);
            return parts.Length == 3 ? $"{parts[0]} ({parts[2]}, since {parts[1]})" : string.Join(' ', parts);
        }
        catch (IOException)
        {
            return "an unknown holder";
        }
    }
}
