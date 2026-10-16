using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Merges an override over a built-in agent entry and checks the result as an entry (launcher spec §4, schemas/launcher.schema.json).</summary>
internal static partial class AgentSections
{
    private static readonly string[] _sessionIds = ["pre-chosen-uuid", "reported", "none"];
    private static readonly string[] _usages = ["claude-code", "codex", "cursor", "none"];
    private static readonly string[] _mcps = ["claude-config-file", "codex-overrides", "none"];

    /// <summary>The built-in entry with <paramref name="over"/>'s named fields laid over it.</summary>
    internal static AgentSection Merge(AgentSection baseline, AgentSection over) => Elements(baseline, over) with
    {
        Label = over.Label ?? baseline.Label,
        Bin = over.Bin ?? baseline.Bin,
        Open = over.Open ?? baseline.Open,
        SessionId = over.SessionId ?? baseline.SessionId,
        Usage = over.Usage ?? baseline.Usage,
        Mcp = over.Mcp ?? baseline.Mcp,
        Override = null,
    };

    private static AgentSection Elements(AgentSection baseline, AgentSection over) => baseline with
    {
        Resume = Present(over.Resume) ? over.Resume : baseline.Resume,
        SessionIdPattern = Present(over.SessionIdPattern) ? over.SessionIdPattern : baseline.SessionIdPattern,
        ReportedId = Present(over.ReportedId) ? over.ReportedId : baseline.ReportedId,
        ProcessName = Present(over.ProcessName) ? over.ProcessName : baseline.ProcessName,
        Transcripts = Present(over.Transcripts) ? over.Transcripts : baseline.Transcripts,
    };

    /// <summary>Checks <paramref name="section"/> as a full entry named <paramref name="id"/> and reads the fields the collector uses.</summary>
    internal static Result<AgentEntry> Entry(string id, AgentSection section)
    {
        if (!AgentId.TryFrom(id, out var agentId))
        {
            return Bad(id, "the id isn't an agent id");
        }

        var problem = Problem(section);
        return problem is not null
            ? Bad(id, problem)
            : new AgentEntry(agentId, section.Label!, section.Bin!, Text(section.ProcessName), Text(section.Transcripts), Strings(section.Resume)) // why: Problem() proved label and bin are set
            {
                IdCapture = Capture(section.ReportedId),
            };
    }

    private static string? Problem(AgentSection s)
    {
        if (s.Label is not { Length: > 0 } || s.Bin is not { Length: > 0 } || s.Open is not { Count: > 0 } || s.SessionId is null)
        {
            return "an entry needs label, bin, open and sessionId";
        }

        return OneOf(s.SessionId, _sessionIds, "sessionId") ?? OneOf(s.Usage, _usages, "usage") ?? OneOf(s.Mcp, _mcps, "mcp")
            ?? Argv(s.Open, "open") ?? Argv(Strings(s.Resume), "resume") ?? Reported(s);
    }

    private static string? Reported(AgentSection s)
    {
        if ((string.Equals(s.SessionId, "pre-chosen-uuid", StringComparison.Ordinal) || string.Equals(s.SessionId, "reported", StringComparison.Ordinal))
            && s.SessionIdPattern.ValueKind != JsonValueKind.String)
        {
            return "sessionIdPattern is required unless sessionId is none";
        }

        if (string.Equals(s.SessionId, "reported", StringComparison.Ordinal) && s.ReportedId.ValueKind != JsonValueKind.Object)
        {
            return "sessionId reported needs reportedId";
        }

        return s.ReportedId.ValueKind == JsonValueKind.Object && AwJson.Deserialize<ReportedIdSection>(
            System.Text.Encoding.UTF8.GetBytes(s.ReportedId.GetRawText()), LauncherConfigFile.Json).IsError ? "reportedId has an unknown key" : null;
    }

    private static string? OneOf(string? value, string[] allowed, string field) =>
        value is null || allowed.Contains(value, StringComparer.Ordinal) ? null : $"{field} '{value}' isn't one of {string.Join(", ", allowed)}";

    internal static string? Argv(IReadOnlyList<string>? argv, string field) =>
        argv?.FirstOrDefault(static e => !Element.IsMatch(e)) is { } bad ? $"{field}: '{bad}' isn't an argument template (closed placeholder set; {{cmd...}} is a whole element)" : null;

    private static bool Present(JsonElement element) => element.ValueKind != JsonValueKind.Undefined;

    private static string? Capture(JsonElement reported) =>
        reported.ValueKind == JsonValueKind.Object && reported.TryGetProperty("capture", out var capture) ? Text(capture) : null;

    private static string? Text(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static IReadOnlyList<string>? Strings(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? [.. element.EnumerateArray().Select(static e => e.GetString() ?? string.Empty)] : null;

    private static Error Bad(string id, string reason) => SindriErrors.LauncherInvalid.With($"agents.{id}: {reason}");

    [GeneratedRegex("^(?:\\{cmd\\.\\.\\.\\}|(?:[^{}]|\\{(?:bin|cwd|title|name|seed|sessionId|mcpConfig)\\})*)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Element { get; }
}
