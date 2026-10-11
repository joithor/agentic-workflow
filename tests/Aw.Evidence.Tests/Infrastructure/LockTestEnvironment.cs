using Aw.Evidence.Infrastructure;
using Aw.Evidence.Infrastructure.Locks;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>DirectoryLocks over a temp state root; never the real ~/.agentic-workflow/locks, and the held flag set explicitly.</summary>
internal static class LockTestEnvironment
{
    internal static LockSettings Settings(TempDirectory root, bool heavyHeld = false) =>
        SettingsFromEnvironment.Read(name => name switch
        {
            "AW_STATE_DIR" => root.Path,
            "AW_HEAVY_LOCK_HELD" => heavyHeld ? "1" : "0",
            _ => null,
        }).Locks;

    internal static DirectoryLocks Locks(TempDirectory root, bool heavyHeld = false, TextWriter? notices = null) =>
        new(Settings(root, heavyHeld), TimeProvider.System, notices ?? TextWriter.Null);
}
