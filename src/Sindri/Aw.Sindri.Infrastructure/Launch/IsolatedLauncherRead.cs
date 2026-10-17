using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Reads launcher.json one section at a time (launcher spec §4): a mistake in one section costs that section only. 'meta' is ignored whatever it holds;
/// 'viewer' and 'host' problems are recorded for attended opens and never reach the collector; a bad 'agents.&lt;id&gt;' entry drops itself; a bad
/// 'sessions' section falls back to the defaults, except that an explicit "enabled": false still holds, so a typo never turns the collector on. A file
/// that isn't a JSON object, or whose schema isn't 1, gives the built-ins and the defaults.
/// </summary>
internal static class IsolatedLauncherRead
{
    private static readonly string[] _topLevel = ["schema", "viewer", "host", "agents", "meta", "sessions"];

    /// <summary>The catalog and the sessions section from the built-ins and an optional user file.</summary>
    /// <param name="builtins">The built-in entries.</param>
    /// <param name="user">The text of launcher.json, or null when the box has none.</param>
    /// <returns>The config; its problems say what was dropped.</returns>
    internal static LauncherConfig Read(byte[] builtins, byte[]? user)
    {
        var baseline = LauncherConfigFile.Read(builtins, user: null).Value;
        if (user is null)
        {
            return baseline;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(user);
        }
        catch (JsonException)
        {
            return baseline with { Problem = "launcher.json isn't valid JSON" };
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
            {
                return baseline with { Problem = "launcher.json needs \"schema\": 1" };
            }

            return Sections(builtins, root);
        }
    }

    private static LauncherConfig Sections(byte[] builtins, JsonElement root)
    {
        var problems = new List<string>();
        var attended = new List<string>();
        foreach (var name in root.EnumerateObject().Select(static p => p.Name).Where(static n => !_topLevel.Contains(n, StringComparer.Ordinal)))
        {
            problems.Add($"unknown key '{name}'");
        }

        Attended<ViewerSection>(root, "viewer", LauncherConfigFile.Viewer, attended);
        Attended<HostSection>(root, "host", LauncherConfigFile.Host, attended);
        var sessions = Sessions(root, problems);
        var agents = Agents(builtins, root, problems);
        return new LauncherConfig(agents, sessions) { Problem = problems.Count == 0 ? null : string.Join("; ", problems), AttendedProblems = attended };
    }

    private static void Attended<T>(JsonElement root, string name, Func<T?, string?> check, List<string> attended)
        where T : class
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return;
        }

        var section = Section<T>(element);
        var problem = section is null ? $"{name} isn't valid" : check(section);
        if (problem is not null)
        {
            attended.Add(problem);
        }
    }

    private static SessionsSection? Sessions(JsonElement root, List<string> problems)
    {
        if (!root.TryGetProperty("sessions", out var element))
        {
            return null;
        }

        var section = Section<SessionsSection>(element);
        if (section is not null && LauncherConfigFile.Sessions(section) is null)
        {
            return section;
        }

        problems.Add("sessions isn't valid; the defaults are used");
        var stayOff = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False;
        return stayOff ? new SessionsSection { Enabled = false } : null;
    }

    private static List<AgentEntry> Agents(byte[] builtins, JsonElement root, List<string> problems)
    {
        var baselineAgents = AwJson.Deserialize<LauncherDocument>(builtins, LauncherConfigFile.Json).Value.Agents ?? [];
        var merged = new Dictionary<string, AgentSection>(baselineAgents, StringComparer.Ordinal);
        if (root.TryGetProperty("agents", out var added) && added.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in added.EnumerateObject())
            {
                Add(property, baselineAgents, merged, problems);
            }
        }
        else if (root.TryGetProperty("agents", out _))
        {
            problems.Add("agents isn't an object");
        }

        var entries = new List<AgentEntry>();
        foreach (var (id, section) in merged)
        {
            var entry = AgentSections.Entry(id, section);
            if (entry.IsSuccess)
            {
                entries.Add(entry.Value);
            }
            else
            {
                problems.Add(entry.Error.Message);
            }
        }

        return entries;
    }

    private static void Add(JsonProperty property, Dictionary<string, AgentSection> baselineAgents, Dictionary<string, AgentSection> merged, List<string> problems)
    {
        var id = property.Name;
        var section = Section<AgentSection>(property.Value);
        var builtIn = baselineAgents.TryGetValue(id, out var baseline);
        if (section is null)
        {
            problems.Add($"agents.{id} isn't valid");
        }
        else if (section.Override == true != builtIn)
        {
            problems.Add($"agents.{id}: " + (builtIn ? "a built-in id needs \"override\": true" : "\"override\": true is for a built-in id"));
        }
        else
        {
            var candidate = builtIn ? AgentSections.Merge(baseline!, section) : section; // why: TryGetValue returned true
            var checkedEntry = AgentSections.Entry(id, candidate);
            if (checkedEntry.IsError)
            {
                problems.Add(checkedEntry.Error.Message);
            }
            else
            {
                merged[id] = candidate;
            }
        }
    }

    private static T? Section<T>(JsonElement element)
        where T : class
    {
        var read = AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(element.GetRawText()), LauncherConfigFile.Json);
        return read.IsError ? null : read.Value;
    }
}
