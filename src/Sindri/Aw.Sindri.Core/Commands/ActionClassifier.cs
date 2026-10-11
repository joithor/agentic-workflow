namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Finds the catalog row a command attempt runs as (API spec §2.1, UI spec E-12), fail-closed: a plain command takes its
/// one row; a classified command takes '&lt;Command&gt;:&lt;Variant&gt;'; an unknown variant, or protected fields on a
/// row that isn't protected, make it protected.
/// </summary>
public static class ActionClassifier
{
    /// <summary>The row <paramref name="command"/> runs as.</summary>
    /// <param name="catalog">The command catalog.</param>
    /// <param name="command">The request record.</param>
    /// <returns>Its row, or a synthetic protected row when it has no row to match.</returns>
    public static CommandRow Classify(ICommandCatalog catalog, object command) =>
        throw new NotSupportedException("red: Classify");
}
