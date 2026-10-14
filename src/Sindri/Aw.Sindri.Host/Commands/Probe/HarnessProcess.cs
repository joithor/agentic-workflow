using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Commands.Probe;

/// <summary>
/// An ancestry pattern the probe pinned (engine spec §6.2): an ancestor is an agent harness when its name as invoked is
/// <paramref name="Name"/>, or its executable's path contains <paramref name="ExecutablePart"/> (Claude Code's binary
/// lives under '…/claude/versions/', whatever it was invoked as).
/// </summary>
/// <param name="Name">The name as invoked (argv[0]'s basename), or null.</param>
/// <param name="ExecutablePart">A part of the executable's path, or null.</param>
internal sealed record HarnessProcess(string? Name, string? ExecutablePart)
{
    /// <summary>Whether <paramref name="name"/> (with <paramref name="executable"/>) is this harness.</summary>
    /// <param name="name">The name as invoked; a path is cut to its basename.</param>
    /// <param name="executable">The executable's path, or null.</param>
    /// <returns>Whether it matches.</returns>
    internal bool Matches(string name, string? executable) =>
        (Name is not null && string.Equals(BaseName(name), Name, StringComparison.Ordinal))
        || (ExecutablePart is not null && executable is not null && executable.Contains(ExecutablePart, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="ancestor"/> is this harness.</summary>
    /// <param name="ancestor">An ancestor.</param>
    /// <returns>Whether it matches.</returns>
    internal bool Matches(ProcessAncestor ancestor) => Matches(ancestor.Name, ancestor.Executable);

    /// <summary>Whether <paramref name="process"/> is this harness.</summary>
    /// <param name="process">A probed ancestor.</param>
    /// <returns>Whether it matches.</returns>
    internal bool Matches(ProbedProcess process) => Matches(process.Name, process.Executable);

    /// <summary>The basename of a name that may be a path.</summary>
    /// <param name="name">The name.</param>
    /// <returns>Its basename.</returns>
    internal static string BaseName(string name) => name[(name.LastIndexOf('/') + 1)..];
}
