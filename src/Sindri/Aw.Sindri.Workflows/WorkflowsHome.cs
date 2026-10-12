namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where the adapter keeps its files under the Sindri home. Engine P0 spike: the desk task tokens, the output facts a
/// desk report stored, and the recipes the worker loads (P2's registry replaces the directory).
/// </summary>
/// <param name="Home">The Sindri home ($AW_SINDRI_HOME).</param>
internal sealed record WorkflowsHome(string Home)
{
    /// <summary>One file per open desk run: '&lt;address with / as ~&gt;/&lt;run token&gt;', holding the task token.</summary>
    public string TasksDirectory => Path.Combine(Home, "workflow", "tasks");

    /// <summary>Output facts by SHA-256, stored 0600 before the completion that references them.</summary>
    public string FactsDirectory => Path.Combine(Home, "workflow", "facts");

    /// <summary>The recipe files, '&lt;id&gt;.yaml'.</summary>
    public string RecipesDirectory => Path.Combine(Home, "recipes");

    /// <summary>
    /// The home as Aw.Sindri.Infrastructure resolves it (SettingsFromEnvironment, internal there): AW_SINDRI_HOME,
    /// else $AW_STATE_DIR/sindri-app, else ~/.agentic-workflow/sindri-app. Spike duplicate; P4 reads it from the host.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The home.</returns>
    public static WorkflowsHome From(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } root
            ? root
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var home = environment("AW_SINDRI_HOME") is { Length: > 0 } explicitHome ? explicitHome : Path.Combine(state, "sindri-app");
        return new WorkflowsHome(Path.GetFullPath(home));
    }
}
