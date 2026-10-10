using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Checks;

/// <summary>A strongly connected group of tasks: every task in it waits, through dependencies, on every other.</summary>
/// <param name="Members">The group's tasks, sorted by id.</param>
/// <param name="Path">One cycle through the group, from its smallest id back to it: t1, t3, t2, t1.</param>
internal sealed record TaskCycle(IReadOnlyList<TaskId> Members, IReadOnlyList<TaskId> Path);
