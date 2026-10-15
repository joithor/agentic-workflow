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
/// one is a no-op. Like the heavy wrapper, a slot whose recorded owner process is gone is reclaimed; a slot with no record is held (see <see cref="NoRecordGrace"/>).
/// </summary>
/// <param name="settings">Where the lock is and how many slots it has.</param>
/// <param name="clock">The clock the wait runs on.</param>
internal sealed class DirectoryHeavySlots(HeavyLockSettings settings, TimeProvider clock) : IHeavySlots
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    /// <summary>How old a slot with no record at all must be before it is taken (the TS sindri's last resort; a live config/lib/locks.sh holder writes none).</summary>
    internal static readonly TimeSpan NoRecordGrace = TimeSpan.FromHours(6);

    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(1);

    /// <summary>Called with a slot's path after it was judged stale and before it is taken over (tests use it to play the other waiter).</summary>
    internal Action<string> BeforeTakeover { get; init; } = static _ => { };

    /// <summary>Called with a slot's path right after our mkdir made it (tests use it to make the owner write fail).</summary>
    internal Action<string> AfterMkdir { get; init; } = static _ => { };

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
                if (!WriteOwner(slot, holder))
                {
                    return SindriErrors.ServiceFailed.With($"can't write the owner record in {slot}, so the slot was released");
                }

                return new HeavySlotLease(slot);
            }
        }

        return (HeavySlotLease?)null;
    }

    private Result<bool> TryTake(string directory)
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

        if (errno == 0)
        {
            AfterMkdir(directory);
        }

        return errno switch
        {
            0 => true,
            Libc.AlreadyExists => false,
            _ => SindriErrors.ServiceFailed.With(string.Create(CultureInfo.InvariantCulture, $"mkdir {directory} failed (errno {errno})")),
        };
    }

    /// <summary>
    /// Removes a slot whose holder is provably dead, once. The record that proves it is our own '&lt;slot&gt;/owner' or the TS sindri's
    /// '&lt;slot&gt;.holder.json' (written beside the slot, and only trusted when it is not older than the slot): a pid that is gone on this host,
    /// as the heavy wrapper judges it. A slot with no record is held: config/lib/locks.sh and ui-evidence write none, and the TS sindri
    /// treats such a slot as live too. Only after <see cref="NoRecordGrace"/> (the TS sindri's 6 h last resort, for a crashed shell holder
    /// nobody will ever release) is a record-less slot taken. The dead record is first renamed to a name of its own, which only one reclaimer can
    /// win; the winner checks it got the record it judged, and puts it back (inside a directory that still exists, so nobody else can have taken
    /// the slot) if a new holder's record was moved instead. False when the slot is held, was taken meanwhile, or can't be removed.
    /// </summary>
    private bool Reclaim(string directory)
    {
        try
        {
            var record = RecordOf(directory);
            if (record is null)
            {
                return clock.GetUtcNow() - new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory), TimeSpan.Zero) > NoRecordGrace
                    && TryRemove(directory);
            }

            if (!Dead(record))
            {
                return false;
            }

            BeforeTakeover(directory);
            var claimed = record.Path + ".reclaim." + Path.GetRandomFileName();
            File.Move(record.Path, claimed);   // atomic: of two reclaimers of one record, the second finds nothing to move
            if (!string.Equals(File.ReadAllText(claimed), record.Text, StringComparison.Ordinal))
            {
                File.Move(claimed, record.Path);   // a new holder's record: the slot changed hands between the judgment and the rename
                return false;
            }

            File.Delete(claimed);
            return TryRemove(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryRemove(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static HolderRecord? RecordOf(string directory)
    {
        var owner = Path.Combine(directory, OwnerFile);
        if (File.Exists(owner))
        {
            var text = File.ReadAllText(owner);
            return int.TryParse(text.Split(' ', 2)[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? new HolderRecord(owner, text, pid, Host: null) : null;
        }

        var beside = directory + ".holder.json";
        if (!File.Exists(beside) || File.GetLastWriteTimeUtc(beside) < Directory.GetLastWriteTimeUtc(directory))
        {
            return null;   // no record, or a leftover from a holder that took the slot without one
        }

        var json = File.ReadAllText(beside);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var number)
                ? new HolderRecord(beside, json, number, root.TryGetProperty("host", out var host) ? host.GetString() : null)
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static bool Dead(HolderRecord record) =>
        (record.Host is null || string.Equals(record.Host, Environment.MachineName, StringComparison.OrdinalIgnoreCase)) && !Alive(record.Pid);

    private sealed record HolderRecord(string Path, string Text, int Pid, string? Host);

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

    private bool WriteOwner(string directory, string holder)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {clock.GetUtcNow().UtcDateTime:yyyy-MM-ddTHH:mm:ssZ} {holder}\n");
        try
        {
            File.WriteAllText(Path.Combine(directory, OwnerFile), line);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = TryRemove(directory);   // a slot without our record would read as nobody's, so don't hold it
            return false;
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
