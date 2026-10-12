namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where the adapter keeps its files under the Sindri home. Engine P4a: the desk task tokens, the output facts a
/// desk report stored, and the recipes the worker loads (P2's registry replaces the directory).
/// </summary>
/// <param name="Home">The Sindri home, as <see cref="TemporalSettings"/> reads it.</param>
internal sealed record WorkflowsHome(string Home)
{
    /// <summary>One file per open desk run: '&lt;address with / as ~&gt;/&lt;run token&gt;', holding the task token.</summary>
    public string TasksDirectory => Path.Combine(Home, "workflow", "tasks");

    /// <summary>Output facts by SHA-256, stored 0600 before the completion that references them.</summary>
    public string FactsDirectory => Path.Combine(Home, "workflow", "facts");

    /// <summary>The recipe files, '&lt;id&gt;.yaml'.</summary>
    public string RecipesDirectory => Path.Combine(Home, "recipes");
}
