namespace Aw.Sindri.Core.Onyx;

/// <summary>The compose project `sindri onyx up` runs: its three files, named so every command gets the same arguments.</summary>
/// <param name="ComposeFile">The pinned docker-compose.yml.</param>
/// <param name="OverrideFile">Sindri's docker-compose.override.yml.</param>
/// <param name="EnvFile">The .env Sindri rendered.</param>
public sealed record ComposeProject(string ComposeFile, string OverrideFile, string EnvFile)
{
    /// <summary>The compose project name every command passes with -p, so Sindri's Onyx never mixes with another stack.</summary>
    public const string Name = "sindri-onyx";

    /// <summary>Gets the variables every compose call must not inherit from the shell: Compose's own COMPOSE_* settings and every key Sindri writes into the .env (the shell wins over the .env, so a stray one would change what the rendered files say).</summary>
    public static IReadOnlyList<string> StrippedVariables { get; } = ["COMPOSE_PROFILES", "COMPOSE_FILE", "COMPOSE_PROJECT_NAME", "COMPOSE_ENV_FILES", "COMPOSE_PATH_SEPARATOR", .. OnyxConfig.EnvKeys];
}
