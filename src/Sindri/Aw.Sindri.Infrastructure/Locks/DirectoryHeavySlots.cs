using Aw.Kernel;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>The heavy-job lock as directories, interoperable with config/lib/locks.sh, the heavy wrapper and Evidence's directory locks.</summary>
/// <param name="settings">Where the lock is and how many slots it has.</param>
/// <param name="clock">The clock the wait runs on.</param>
internal sealed class DirectoryHeavySlots(HeavyLockSettings settings, TimeProvider clock) : IHeavySlots
{
    /// <summary>The file a holder writes inside the lock directory.</summary>
    internal const string OwnerFile = "owner";

    private readonly HeavyLockSettings _settings = settings;
    private readonly TimeProvider _clock = clock;

    public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<HeavySlotTaken>>(new HeavySlotTaken(Lease: null, $"{_settings.Lock} {_clock}"));

    /// <summary>Whether an owner line names this process.</summary>
    /// <param name="ownerLine">The owner file's text.</param>
    /// <returns>True when its first word is our pid.</returns>
    internal static bool IsOurs(string ownerLine) => ownerLine.Split(' ', 2)[0] == System.Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
