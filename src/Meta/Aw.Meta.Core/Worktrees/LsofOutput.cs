#pragma warning disable MA0182 // red stub: used by the implementation
namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads the field output of "lsof -F pcn" (the macOS fallback of the process scan).</summary>
internal static class LsofOutput
{
    /// <summary>Groups the p (pid), c (command) and n (name) fields into one location per process.</summary>
    /// <param name="output">The lsof output.</param>
    /// <returns>The processes with the absolute paths they hold.</returns>
    internal static IReadOnlyList<ProcessLocation> Parse(string output) => throw new NotSupportedException();
}
