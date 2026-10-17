using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The shape of launcher.json and builtins.json (schemas/launcher.schema.json): unknown keys are errors, in every section.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LauncherDocument(int Schema)
{
    public ViewerSection? Viewer { get; init; }

    public HostSection? Host { get; init; }

    public Dictionary<string, AgentSection>? Agents { get; init; }

    public MetaSection? Meta { get; init; }

    public SessionsSection? Sessions { get; init; }
}
