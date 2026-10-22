namespace Aw.Sindri.Core.Ports;

/// <summary>The git tree hash of 'recipes/sindri' as this build embeds it, and as the checkout the app runs from holds it at HEAD (setup-once spec §5.3).</summary>
/// <param name="Embedded">The tree hash recorded in the build's stamp.</param>
/// <param name="Head">The checkout's HEAD tree hash, or null when the app isn't run from a checkout that has the folder.</param>
public sealed record RecipeTrees(string Embedded, string? Head);

/// <summary>Where doctor learns whether the embedded recipe set is HEAD's: a read of the build stamp and of git, starting nothing.</summary>
public interface IRecipeBuild
{
    /// <summary>The two tree hashes.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The trees, or null when this build has no stamp (it wasn't published by sindri-app).</returns>
    public ValueTask<RecipeTrees?> Read(CancellationToken cancellationToken);
}
