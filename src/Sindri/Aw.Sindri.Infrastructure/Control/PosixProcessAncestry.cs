using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>Process ancestry on macOS and Linux.</summary>
internal sealed class PosixProcessAncestry : IProcessAncestry
{
    public IReadOnlyList<ProcessAncestor> Ancestors(int pid, int max) => throw new NotSupportedException("red: walk the parent pids");
}
