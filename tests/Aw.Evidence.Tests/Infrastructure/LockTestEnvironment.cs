using System.Globalization;
using Aw.Evidence.Infrastructure;
using Aw.Evidence.Infrastructure.Locks;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// DirectoryLocks over a temp state root; never the real ~/.agentic-workflow/locks. The held flag and the slot count
/// are set explicitly, so the test process's own AW_HEAVY_LOCK_HELD (the heavy wrapper sets it) and AW_HEAVY_SLOTS
/// never leak in.
/// </summary>
internal static class LockTestEnvironment
{
    internal static LockSettings Settings(TempDirectory root, bool heavyHeld = false, string slots = "2") =>
        SettingsFromEnvironment.Read(name => name switch
        {
            "AW_STATE_DIR" => root.Path,
            "AW_HEAVY_LOCK_HELD" => heavyHeld ? "1" : "0",
            "AW_HEAVY_SLOTS" => slots,
            _ => null,
        }).Locks;

    internal static DirectoryLocks Locks(TempDirectory root, bool heavyHeld = false, TextWriter? notices = null, string slots = "2") =>
        new(Settings(root, heavyHeld, slots), TimeProvider.System, notices ?? TextWriter.Null);

    /// <summary>Slot k of the heavy-job lock: slot 1 is the lock path itself, slot k is '&lt;lock&gt;.k'.</summary>
    internal static string HeavySlot(TempDirectory root, int k) =>
        k == 1 ? Settings(root).HeavyJob : string.Create(CultureInfo.InvariantCulture, $"{Settings(root).HeavyJob}.{k}");
}
