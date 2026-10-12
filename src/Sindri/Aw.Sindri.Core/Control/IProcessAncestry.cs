namespace Aw.Sindri.Core.Control;

/// <summary>
/// A process's ancestors (port): how the CLI sees that an agent harness started it (API spec §6, "a parent process that
/// is an agent harness also counts").
/// </summary>
public interface IProcessAncestry
{
    /// <summary>
    /// The ancestors of <paramref name="pid"/>, nearest first (its parent, then the parent's parent, …), ending at pid 1
    /// or after <paramref name="max"/> entries. A level that can't be read ends the list there, so an unreadable
    /// ancestry is an empty one, never an error (P1 plan G10: the agent variables still apply).
    /// </summary>
    /// <param name="pid">The process whose ancestors to read.</param>
    /// <param name="max">The most entries to return.</param>
    /// <returns>The ancestors read.</returns>
    public IReadOnlyList<ProcessAncestor> Ancestors(int pid, int max);
}
