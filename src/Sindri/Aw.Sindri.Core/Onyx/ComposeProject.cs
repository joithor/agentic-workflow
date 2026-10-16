namespace Aw.Sindri.Core.Onyx;

/// <summary>The compose project `sindri onyx up` runs: its three files, named so every command gets the same arguments.</summary>
/// <param name="ComposeFile">The pinned docker-compose.yml.</param>
/// <param name="OverrideFile">Sindri's docker-compose.override.yml.</param>
/// <param name="EnvFile">The .env Sindri rendered.</param>
public sealed record ComposeProject(string ComposeFile, string OverrideFile, string EnvFile)
{
    /// <summary>The compose project name every command passes with -p, so Sindri's Onyx never mixes with another stack.</summary>
    public const string Name = "sindri-onyx";
}
