namespace Aw.Context.Infrastructure.Extract;

/// <summary>The machine the sandbox command is built for.</summary>
/// <param name="IsLinux">Linux (bwrap).</param>
/// <param name="IsMac">macOS (sandbox-exec).</param>
/// <param name="Home">The user's home: its credential stores are hidden.</param>
/// <param name="RuntimeDir">$XDG_RUNTIME_DIR, or null.</param>
/// <param name="IsRootOwned">Whether an absolute path is a root-owned executable file (a tool a user install dropped on PATH never counts).</param>
/// <param name="Kind">What a path is.</param>
/// <param name="RealPath">A path with its links resolved, or the path itself when it doesn't exist.</param>
internal sealed record SandboxHost(bool IsLinux, bool IsMac, string Home, string? RuntimeDir, Func<string, bool> IsRootOwned, Func<string, PathKind> Kind, Func<string, string> RealPath);
