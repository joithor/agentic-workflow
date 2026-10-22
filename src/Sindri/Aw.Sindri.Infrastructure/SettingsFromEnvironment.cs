namespace Aw.Sindri.Infrastructure;

/// <summary>
/// Reads this package's settings from the environment that Composition passes in. The C# app keeps its state apart from
/// the TypeScript sindri's ($AW_STATE_DIR/sindri) until the switch: AW_SINDRI_HOME, else $AW_STATE_DIR/sindri-app, else
/// ~/.agentic-workflow/sindri-app. The profile is AW_SINDRI_PROFILE, else &lt;home&gt;/profile/profile.yaml. The prover
/// rule reads TMPDIR (else the platform's temp directory) and the same state root.
/// </summary>
internal static class SettingsFromEnvironment
{
    internal static SindriSettings Read(Func<string, string?> environment)
    {
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome
            ? explicitHome
            : Path.Combine(StateRoot(environment), "sindri-app");
        var profile = environment("AW_SINDRI_PROFILE") is { Length: > 0 } explicitProfile
            ? explicitProfile
            : Path.Combine(home, "profile", "profile.yaml");
        return new SindriSettings(Path.GetFullPath(home), Path.GetFullPath(profile))
        {
            TempDirectory = environment("TMPDIR") is { Length: > 0 } temp ? Path.GetFullPath(temp) : Path.GetTempPath(),
            StateDirectory = Path.GetFullPath(StateRoot(environment)),
        };
    }

    /// <summary>The state root: AW_STATE_DIR, else ~/.agentic-workflow.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The directory.</returns>
    internal static string StateRoot(Func<string, string?> environment) =>
        environment("AW_STATE_DIR") is { Length: > 0 } root
            ? root
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
}
