using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>The rules on the packet's own structure: unique ids, workstreams, and the dependency graph.</summary>
internal static class StructureChecks
{
    internal static void Run(Packet packet, CheckList checks)
    {
        var plan = packet.Tasks;
        checks.Rule(PacketRules.UniqueIds, "every id is unique", DuplicateIds(packet));
        var workstreams = plan.Workstreams.Select(static w => w.Id).ToHashSet();
        checks.Rule(
            PacketRules.TaskInWorkstream,
            "every task is in a declared workstream",
            plan.Tasks.Where(t => !workstreams.Contains(t.Workstream)).Select(static t => (
                $"task {t.Id.Value}",
                $"workstream '{t.Workstream.Value}' isn't declared in the task plan",
                "Declare the workstream under 'workstreams', or move the task to one that is.")));
        var known = plan.Tasks.Select(static t => t.Id).ToHashSet();
        checks.Rule(
            PacketRules.KnownDependencies,
            "every dependency names a task",
            plan.Tasks.SelectMany(t => t.Deps.Where(d => !known.Contains(d)).Select(d => (
                $"task {t.Id.Value}",
                $"depends on '{d.Value}', which isn't a task in this packet",
                "Remove the dependency, or add the task it names."))));
        checks.Rule(
            PacketRules.Acyclic,
            "the task graph has no cycle",
            new TaskGraph(plan.Tasks).Cycles().Select(static c => (
                $"task {c.Members[0].Value}",
                c.Members.Count == 1
                    ? $"task {c.Members[0].Value} depends on itself"
                    : $"tasks {string.Join(", ", c.Members.Select(static id => id.Value))} form a cycle: {string.Join(" -> ", c.Path.Select(static id => id.Value))}",
                "Remove dependencies until no task in the group waits on itself, or merge the tasks.")));
    }

    private static IEnumerable<(string, string, string)> DuplicateIds(Packet packet)
    {
        var tasks = packet.Tasks.Tasks;
        IEnumerable<(string Kind, string Id)> ids =
        [
            .. tasks.Select(static t => ("task", t.Id.Value)),
            .. packet.Tasks.Workstreams.Select(static w => ("workstream", w.Id.Value)),
            .. packet.Decisions.Questions.Select(static q => ("question", q.Id.Value)),
            .. tasks.SelectMany(static t => t.Acceptance).Select(static e => ("example", e.Id.Value)),
        ];
        return ids
            .GroupBy(static x => (x.Kind, x.Id))
            .Where(static g => g.Skip(1).Any())
            .Select(static g => (
                $"{g.Key.Kind} {g.Key.Id}",
                $"{g.Count()} {g.Key.Kind}s share the id '{g.Key.Id}'",
                $"Give each {g.Key.Kind} its own id."));
    }
}
