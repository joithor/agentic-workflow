using System.Globalization;
using System.Runtime.Versioning;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A throwaway fleet directory in the temp folder with hook-shaped state files.</summary>
[UnsupportedOSPlatform("windows")]
internal sealed class TempFleet : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("aw-fleet").FullName;

    public void Write(string provider, string session, DateTimeOffset at, bool marker = true)
    {
        var directory = Path.Combine(Root, provider);
        _ = Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (marker)
        {
            File.WriteAllText(Path.Combine(directory, FleetStateFiles.Marker), string.Empty);
        }

        var stamp = at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(directory, session + ".json"),
            $$"""{ "schema": 1, "provider": "{{provider}}", "sessionId": "{{session}}", "event": "Stop", "state": "idle", "at": "{{stamp}}", "cwd": null, "transcript": null, "agentPid": null, "agentStartTicks": null, "env": {} }""");
    }

    public FleetStateFiles Files(bool pruning) => new(Root, [AgentId.From("claude-code"), AgentId.From("codex")], pruning);

    public List<string> Names(string provider) =>
        [.. Directory.EnumerateFiles(Path.Combine(Root, provider), "*.json").Select(static f => Path.GetFileName(f)).Order(StringComparer.Ordinal)];

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
