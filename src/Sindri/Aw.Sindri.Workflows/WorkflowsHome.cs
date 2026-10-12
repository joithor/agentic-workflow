namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where the adapter keeps its files under the Sindri home: the facts a start or a desk report stored, and the recipes
/// the stub <see cref="HomeApprovedRecipes"/> reads until P2's registry replaces the directory.
/// </summary>
/// <param name="Home">The Sindri home, as <see cref="TemporalSettings"/> reads it.</param>
internal sealed record WorkflowsHome(string Home)
{
    /// <summary>Facts by SHA-256, stored 0600 before the start or the completion that references them.</summary>
    public string FactsDirectory => Path.Combine(Home, "workflow", "facts");

    /// <summary>The API's nonce table, one owner-only file per nonce (engine P4 unit (c), until API P1's table).</summary>
    public string NoncesDirectory => Path.Combine(Home, "workflow", "nonces");

    /// <summary>The recipe files, '&lt;id&gt;.yaml'.</summary>
    public string RecipesDirectory => Path.Combine(Home, "recipes");
}
