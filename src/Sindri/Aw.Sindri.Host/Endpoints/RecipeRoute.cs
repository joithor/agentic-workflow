using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The '{id}@{hash}' route segment's hash (engine spec §6.3): 'sha256:' and 64 hex digits, or the 64 digits alone.</summary>
internal static class RecipeRoute
{
    private const string Scheme = "sha256:";

    /// <summary>The hash a route names; an invalid one throws Vogen's validation exception, which the Host maps to a 400 (SND-CLI-003).</summary>
    /// <param name="hash">The route value.</param>
    /// <returns>The hash.</returns>
    internal static RecipeHash Hash(string hash) => RecipeHash.From(hash.StartsWith(Scheme, StringComparison.Ordinal) ? hash : Scheme + hash);
}
