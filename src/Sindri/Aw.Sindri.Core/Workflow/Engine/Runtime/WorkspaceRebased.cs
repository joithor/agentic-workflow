namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>How <see cref="IWorkspaceRebase.Rebase"/> ended: on a new head, or on a conflict.</summary>
public sealed class WorkspaceRebased
{
    /// <summary>Initializes a rebase that landed.</summary>
    /// <param name="head">The workspace's new head.</param>
    public WorkspaceRebased(string head)
    {
        Head = head;
        Conflicts = [];
    }

    /// <summary>Initializes a rebase that conflicted.</summary>
    /// <param name="conflicts">The paths git could not merge.</param>
    public WorkspaceRebased(IReadOnlyList<string> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        Conflicts = conflicts;
    }

    /// <summary>Gets the new head, or null when the rebase conflicted.</summary>
    public string? Head { get; }

    /// <summary>Gets the conflicted paths; empty when the rebase landed.</summary>
    public IReadOnlyList<string> Conflicts { get; }
}
