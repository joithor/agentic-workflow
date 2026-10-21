using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-MAP-001: every surface sits in exactly one workstream; dependencies exist and do not cycle.</summary>
    private static void Workstreams(ScopeContext c, RuleSink s)
    {
        if (!s.Need("scopeMap"))
        {
            return;
        }

        var workstreams = ScopeJson.Items(c.Facts["scopeMap"], "workstreams").ToList();
        var ids = workstreams.Select(static w => ScopeJson.Str(w, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var placed = workstreams.SelectMany(static w => ScopeJson.Strings(w, "surfaces")).ToList();
        var surfaceIds = ScopeJson.Items(c.Facts["scopeMap"], "surfaces").Select(static x => ScopeJson.Str(x, "id")).OfType<string>().ToList();
        foreach (var id in surfaceIds)
        {
            var times = placed.Count(p => string.Equals(p, id, StringComparison.Ordinal));
            if (times != 1)
            {
                s.Fail(id, times == 0 ? $"surface {id} is in no workstream" : $"surface {id} is in {times.ToString(CultureInfo.InvariantCulture)} workstreams", "Place every surface in exactly one workstream.");
            }
        }

        foreach (var stray in placed.Where(p => !surfaceIds.Contains(p, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            s.Fail(stray, $"a workstream names {stray}, which is not a surface of the map", "Name only surfaces of the map.");
        }

        foreach (var workstream in workstreams)
        {
            foreach (var dep in ScopeJson.Strings(workstream, "deps").Where(d => !ids.Contains(d)))
            {
                s.Fail(ScopeJson.Str(workstream, "id"), $"workstream {ScopeJson.Str(workstream, "id")} depends on {dep}, which does not exist", "Depend only on workstreams of the map.");
            }
        }

        if (HasCycle(workstreams) is { } cycle)
        {
            s.Fail(cycle, $"workstream {cycle} depends on itself through its dependencies", "Break the cycle: a workstream cannot wait on a workstream that waits on it.");
        }
    }

    private static string? HasCycle(List<JsonNode> workstreams)
    {
        var deps = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var workstream in workstreams)
        {
            if (ScopeJson.Str(workstream, "id") is { } id)
            {
                deps[id] = [.. ScopeJson.Strings(workstream, "deps")];
            }
        }

        var done = new HashSet<string>(StringComparer.Ordinal);
        var path = new HashSet<string>(StringComparer.Ordinal);

        string? Visit(string id)
        {
            if (path.Contains(id))
            {
                return id;
            }

            if (!done.Add(id) || !deps.TryGetValue(id, out var next))
            {
                return null;
            }

            _ = path.Add(id);
            foreach (var dep in next)
            {
                if (Visit(dep) is { } found)
                {
                    return found;
                }
            }

            _ = path.Remove(id);
            return null;
        }

        return deps.Keys.Select(Visit).FirstOrDefault(static f => f is not null);
    }
}
