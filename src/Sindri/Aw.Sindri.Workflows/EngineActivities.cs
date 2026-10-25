namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The activities the engine itself runs around an instance (engine P7): the gates before a decision and the claim cleanup after an ending.</summary>
/// <param name="Gates">EvaluateGates, a local activity before a decision with gates.</param>
/// <param name="Cleanup">ReleaseClaim, after any ending.</param>
/// <param name="Children">LoadChildren, a local activity that reads the children the approval pinned.</param>
/// <param name="Heavy">TakeHeavy and ReleaseHeavy, the heavy resource's allocator (LN-WF3).</param>
/// <param name="Linked">LiveLinked, which finds the live instances linked to an ending one (LN-WF1).</param>
/// <param name="Literals">StoreLiteralFacts, a local activity that writes a child's literal inputs to the fact store at its start.</param>
internal sealed record EngineActivities(GateActivities Gates, CleanupActivities Cleanup, ChildActivities Children, HeavyActivities Heavy, LiteralFactActivities Literals, LinkedActivities Linked);
