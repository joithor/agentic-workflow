namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>What a child's change does to its parent (spec §5.7).</summary>
public enum ParentEffectKind
{
    /// <summary>Nothing (a park with 'onParked: ignore').</summary>
    Ignore = 0,

    /// <summary>Push a park mirroring the child's reason and question.</summary>
    MirrorPark = 1,

    /// <summary>Push an 'escalated (child)' frame.</summary>
    Escalate = 2,

    /// <summary>Pop the frame mirroring that child, back to its wait (the same frame and child).</summary>
    Release = 3,

    /// <summary>The parent is busy: the row stays pending until the parent can take it.</summary>
    Hold = 4,
}
