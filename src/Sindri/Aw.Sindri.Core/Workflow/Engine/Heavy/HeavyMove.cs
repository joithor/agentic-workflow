namespace Aw.Sindri.Core.Workflow.Engine.Heavy;

/// <summary>What the interpreter does about the instance's heavy-job slot at one point of its loop (LN-WF3).</summary>
public enum HeavyMove
{
    /// <summary>Nothing: no slot is taken or given up here.</summary>
    None = 0,

    /// <summary>The instance waits for the heavy resource: take a slot, then grant the wait.</summary>
    Acquire = 1,

    /// <summary>A Step is about to run off the desk in an instance that is in its heavy phase: make sure a slot is held (a no-op when it is, a take after a worker restart).</summary>
    Ensure = 2,

    /// <summary>Give the slot up: the instance is parked, taken over, at a desk Step, or ended.</summary>
    Release = 3,
}
